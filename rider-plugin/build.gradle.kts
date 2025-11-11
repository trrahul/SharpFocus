import org.jetbrains.intellij.platform.gradle.IntelliJPlatformType
import org.jetbrains.intellij.platform.gradle.TestFrameworkType

plugins {
    id("java")
    id("org.jetbrains.kotlin.jvm") version "2.2.0"
    id("org.jetbrains.intellij.platform") version "2.10.4"
}

group = "com.rahultr.sharpfocus"
version = "0.1.1"

repositories {
    mavenCentral()

    intellijPlatform {
        defaultRepositories()
    }
}

val riderSdkVersion = "2025.2.4"

kotlin {
    jvmToolchain(21)
}

sourceSets {
    main {
        resources {
            srcDir("src/rider/main/resources")
        }
        java {
            srcDir("src/rider/main/kotlin")
        }
    }
}

dependencies {
    intellijPlatform {
        rider(riderSdkVersion)

        // Bundled plugins
        bundledPlugin("com.intellij.css")

        // Test framework
        testFramework(TestFrameworkType.Platform)
    }

    // LSP4J for Language Server Protocol support
    implementation("org.eclipse.lsp4j:org.eclipse.lsp4j:0.21.1")
    implementation("org.eclipse.lsp4j:org.eclipse.lsp4j.jsonrpc:0.21.1")

    testImplementation("junit:junit:4.13.2")
}

intellijPlatform {
    pluginConfiguration {
        name = "SharpFocus"
        version = project.version.toString()

        ideaVersion {
            sinceBuild = "251"
            untilBuild = "253.*"
        }
    }

    instrumentCode = true

    publishing {
        token = project.findProperty("publishToken")?.toString()
    }
}

// Task to bundle language server binaries
val bundleLanguageServer by tasks.registering(Exec::class) {
    group = "build"
    description = "Builds and bundles the language server for all platforms"

    val isWindows = System.getProperty("os.name").lowercase().contains("windows")
    val scriptPath = file("build-scripts/bundle-server.ps1").absolutePath
    val outputDir = file("src/rider/main/resources/server")
    val sourceDir = file("../src")

    // Only rebuild if C# source files are newer than output
    inputs.dir(sourceDir).withPathSensitivity(PathSensitivity.RELATIVE)
    outputs.dir(outputDir)

    if (isWindows) {
        commandLine("pwsh", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-Configuration", "Release")
    } else {
        commandLine("pwsh", scriptPath, "-Configuration", "Release")
    }

    doFirst {
        println("Bundling language server for all platforms...")
    }
}

// Task to bundle language server for quick local development (current platform only)
val bundleLanguageServerQuick by tasks.registering(Exec::class) {
    group = "build"
    description = "Quickly builds and bundles the language server for the current platform only"

    val isWindows = System.getProperty("os.name").lowercase().contains("windows")
    val scriptPath = file("build-scripts/bundle-server-quick.ps1").absolutePath
    val outputDir = file("src/rider/main/resources/server")
    val sourceDir = file("../src")

    // Only rebuild if C# source files are newer than output
    inputs.dir(sourceDir).withPathSensitivity(PathSensitivity.RELATIVE)
    outputs.dir(outputDir)

    if (isWindows) {
        commandLine("pwsh", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-Configuration", "Debug")
    } else {
        commandLine("pwsh", scriptPath, "-Configuration", "Debug")
    }

    doFirst {
        println("Bundling language server for current platform (quick build)...")
    }
}

// Only bundle server when building the full plugin distribution
tasks.named("buildPlugin") {
    dependsOn(bundleLanguageServer)
}

// Make processResources depend on bundleLanguageServer ONLY for production builds
// For development, skip server bundling to speed up iteration
val skipServerBundle = project.findProperty("skipServerBundle")?.toString()?.toBoolean() ?: false

if (!skipServerBundle) {
    tasks.named("processResources") {
        dependsOn(bundleLanguageServer)
    }
}

// For development (prepareSandbox), don't auto-bundle - developers can run bundleLanguageServerQuick manually
// This avoids rebuilding the server every time you change Kotlin code
// Uncomment the line below if you want automatic bundling during development:
// tasks.named("prepareSandbox") {
//     dependsOn(bundleLanguageServerQuick)
// }

// Gradle wrapper task
tasks.wrapper {
    gradleVersion = "8.13"
    distributionType = Wrapper.DistributionType.ALL
}

tasks {
    buildSearchableOptions {
        enabled = false
    }

    prepareJarSearchableOptions {
        enabled = false
    }

    compileKotlin {
        compilerOptions {
            jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_21)
            freeCompilerArgs.add("-Xjvm-default=all")
        }
    }

    compileTestKotlin {
        compilerOptions {
            jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_21)
        }
    }
}
