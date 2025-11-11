package com.jetbrains.rider.plugins.sharpfocus.lsp

import com.intellij.openapi.Disposable
import com.google.gson.Gson
import com.intellij.openapi.components.Service
import com.intellij.openapi.diagnostic.logger
import com.intellij.openapi.project.Project
import org.eclipse.lsp4j.*
import org.eclipse.lsp4j.jsonrpc.Launcher
import org.eclipse.lsp4j.launch.LSPLauncher
import org.eclipse.lsp4j.services.LanguageServer
import java.io.File
import java.nio.file.Paths
import java.nio.file.Files
import java.nio.file.StandardCopyOption
import java.util.concurrent.CompletableFuture
import java.util.concurrent.TimeUnit

/**
 * Manages the lifecycle of the SharpFocus Language Server.
 */
@Service(Service.Level.PROJECT)
class SharpFocusLanguageServerManager(private val project: Project) : Disposable {

    private val logger = logger<SharpFocusLanguageServerManager>()

    private var process: Process? = null
    private var languageServer: SharpFocusLanguageServerAPI? = null
    private var launcher: Launcher<SharpFocusLanguageServerAPI>? = null

    private val gson = Gson()

    @Volatile
    private var isStarting = false

    @Volatile
    private var isRunning = false

    companion object {
        fun getInstance(project: Project): SharpFocusLanguageServerManager {
            return project.getService(SharpFocusLanguageServerManager::class.java)
        }

        private const val SERVER_EXE_NAME = "SharpFocus.LanguageServer.exe"
        private const val SERVER_BINARY_NAME = "SharpFocus.LanguageServer"
    }

    /**
     * Starts the language server if not already running.
     */
    fun start(): CompletableFuture<Void> {
        if (isRunning) {
            logger.info("Language server already running")
            return CompletableFuture.completedFuture(null)
        }

        if (isStarting) {
            logger.warn("Language server is already starting")
            return CompletableFuture.completedFuture(null)
        }

        isStarting = true

        return CompletableFuture.supplyAsync {
            try {
                val serverPath = findServerPath()
                if (serverPath == null) {
                    logger.error("Could not find language server DLL")
                    throw IllegalStateException("Language server not found")
                }

                logger.info("Starting language server at: $serverPath")

                val command = listOf(serverPath)
                val processBuilder = ProcessBuilder(command)
                processBuilder.redirectErrorStream(false)

                process = processBuilder.start()

                startProcessOutputLogger(process!!)

                val client = SharpFocusLanguageClient()

                @Suppress("UNCHECKED_CAST")
                launcher = org.eclipse.lsp4j.jsonrpc.Launcher.createLauncher(
                    client,
                    SharpFocusLanguageServerAPI::class.java,
                    process!!.inputStream,
                    process!!.outputStream
                ) as Launcher<SharpFocusLanguageServerAPI>

                languageServer = launcher!!.remoteProxy

                launcher!!.startListening()

                val initParams = InitializeParams().apply {
                    processId = ProcessHandle.current().pid().toInt()
                    project.basePath?.let { basePath ->
                        workspaceFolders = listOf(
                            WorkspaceFolder().apply {
                                uri = "file://$basePath"
                                name = project.name
                            }
                        )
                    }
                    capabilities = ClientCapabilities().apply {
                        textDocument = TextDocumentClientCapabilities()
                        workspace = WorkspaceClientCapabilities().apply {
                            applyEdit = true
                            workspaceEdit = WorkspaceEditCapabilities().apply {
                                documentChanges = true
                            }
                        }
                    }
                }

                val initResult = languageServer!!.initialize(initParams).get(30, TimeUnit.SECONDS)
                logger.info("Language server initialized: ${initResult.serverInfo?.name}")

                languageServer!!.initialized(InitializedParams())

                isRunning = true
                logger.info("Language server started successfully")

            } catch (e: Exception) {
                logger.error("Failed to start language server", e)
                cleanup()
                throw e
            } finally {
                isStarting = false
            }
            null
        }
    }

    /**
     * Stops the language server.
     */
    fun stop(): CompletableFuture<Void> {
        if (!isRunning) {
            return CompletableFuture.completedFuture(null)
        }

        return CompletableFuture.runAsync {
            try {
                logger.info("Stopping language server")

                languageServer?.shutdown()?.get(5, TimeUnit.SECONDS)
                languageServer?.exit()

                process?.destroy()
                process?.waitFor(5, TimeUnit.SECONDS)

                if (process?.isAlive == true) {
                    logger.warn("Force killing language server process")
                    process?.destroyForcibly()
                }

                logger.info("Language server stopped")
            } catch (e: Exception) {
                logger.error("Error stopping language server", e)
            } finally {
                cleanup()
            }
        }
    }

    /**
     * Restarts the language server.
     */
    fun restart(): CompletableFuture<Void> {
        return stop().thenCompose { start() }
    }

    /**
     * Gets the language server instance.
     */
    fun getServer(): SharpFocusLanguageServerAPI? = languageServer

    /**
     * Gets the launcher instance for custom requests.
     */
    fun getLauncher(): Launcher<SharpFocusLanguageServerAPI>? = launcher

    /**
     * Execute a command on the language server using the standard LSP workspace/executeCommand method.
     * This is the standard way to extend LSP functionality and is properly supported by LSP4J.
     *
     * @param command The command identifier (e.g., "sharpfocus.focusMode")
     * @param arguments The command arguments
     * @return CompletableFuture with the response, or null if server not running
     */
    fun executeCommand(command: String, arguments: List<Any>): CompletableFuture<Any?> {
        if (!isRunning || languageServer == null) {
            logger.warn("Cannot execute command: server not running")
            return CompletableFuture.completedFuture(null)
        }

        try {
            logger.info("Executing command: $command with ${arguments.size} arguments")

            val params = ExecuteCommandParams().apply {
                this.command = command
                this.arguments = arguments
            }

            val future = languageServer!!.workspaceService.executeCommand(params)

            future.thenApply { response ->
                logger.info("Received command response: ${response?.javaClass?.name ?: "null"}")
                if (response != null) {
                    logger.info("Response content: $response")
                }
                response
            }

            return future
        } catch (e: Exception) {
            logger.error("Error executing command", e)
            return CompletableFuture.completedFuture(null)
        }
    }

    /**
     * Sends a custom LSP request using the typed API method.
     *
     * @param request The focus mode request parameters
     * @return CompletableFuture with the response parsed as FocusModeResponse
     */
    fun focusMode(request: FocusModeRequest): CompletableFuture<FocusModeResponse?> {
        if (!isRunning || languageServer == null) {
            logger.warn("Cannot send focus mode request: server not running")
            return CompletableFuture.completedFuture(null)
        }

        return try {
            logger.info(
                "Sending focus mode request for ${request.textDocument.uri} @ ${request.position.line}:${request.position.character}"
            )

            // Use the typed API method directly - LSP4J will handle serialization/deserialization
            languageServer!!.focusMode(request)
                .thenApply { response ->
                    if (response == null) {
                        logger.warn("Focus mode response was null")
                        return@thenApply null
                    }

                    logger.info("=== FOCUS MODE RESPONSE RECEIVED ===")
                    logger.info("Focused place: ${response.focusedPlace.name} (kind: ${response.focusedPlace.kind})")
                    logger.info("  Position: line ${response.focusedPlace.range.start.line}:${response.focusedPlace.range.start.character}")
                    logger.info("Relevant ranges: ${response.relevantRanges.size}")
                    response.relevantRanges.forEachIndexed { index, range ->
                        logger.info("  [$index] line ${range.start.line}:${range.start.character} to ${range.end.line}:${range.end.character}")
                    }
                    logger.info("Container ranges: ${response.containerRanges.size}")

                    if (response.backwardSlice != null) {
                        val bs = response.backwardSlice!!
                        logger.info("Backward slice: ${bs.sliceRanges.size} ranges, ${bs.sliceRangeDetails?.size ?: 0} details")
                        bs.sliceRangeDetails?.forEachIndexed { index, detail ->
                            logger.info("  [$index] ${detail.relation} - ${detail.place.name} at line ${detail.range.start.line}:${detail.range.start.character}")
                        }
                    } else {
                        logger.info("Backward slice: null")
                    }

                    if (response.forwardSlice != null) {
                        val fs = response.forwardSlice!!
                        logger.info("Forward slice: ${fs.sliceRanges.size} ranges, ${fs.sliceRangeDetails?.size ?: 0} details")
                        fs.sliceRangeDetails?.forEachIndexed { index, detail ->
                            logger.info("  [$index] ${detail.relation} - ${detail.place.name} at line ${detail.range.start.line}:${detail.range.start.character}")
                        }
                    } else {
                        logger.info("Forward slice: null")
                    }
                    logger.info("=== END RESPONSE ===")

                    response
                }.exceptionally { ex ->
                    logger.error("Focus mode request failed", ex)
                    null
                }
        } catch (e: Exception) {
            logger.error("Error sending focus mode request", e)
            CompletableFuture.completedFuture(null)
        }
    }

    /**
     * Checks if the server is running.
     */
    fun isServerRunning(): Boolean = isRunning

    /**
     * Starts a background thread to log process output for debugging.
     */
    private fun startProcessOutputLogger(process: Process) {
        // Log stderr in a separate thread
        Thread {
            try {
                process.errorStream.bufferedReader().use { reader ->
                    reader.lineSequence().forEach { line ->
                        if (line.isNotBlank()) {
                            logger.warn("[LS stderr] $line")
                        }
                    }
                }
            } catch (e: Exception) {
                logger.debug("Error reading process stderr: ${e.message}")
            }
        }.apply {
            isDaemon = true
            name = "SharpFocus-LS-stderr"
            start()
        }

        logger.info("Language server process started (PID: ${process.pid()})")
    }

    private fun extractBundledServer(platform: String): String? {
        try {
            val tmpDir = Files.createTempDirectory("sharpfocus-server-$platform")
            logger.info("Created extraction directory: $tmpDir")

            val executableName = when {
                platform.contains("win") -> SERVER_EXE_NAME
                else -> SERVER_BINARY_NAME
            }

            val resourcePath = "/server/$platform/$executableName"
            logger.info("Looking for bundled server at: $resourcePath")

            try {
                val classLoader = this::class.java.classLoader
                val serverDirPath = "server/$platform"
                val serverDirUrl = classLoader.getResource(serverDirPath)
                if (serverDirUrl != null) {
                    logger.info("Server directory found at: $serverDirUrl")
                    if (serverDirUrl.protocol == "jar") {
                        val jarPath = serverDirUrl.path.substringBefore("!")
                        logger.info("JAR path: $jarPath")
                        try {
                            val jarFile = java.util.jar.JarFile(jarPath.removePrefix("file:"))
                            val entries = jarFile.entries().asSequence()
                                .filter { it.name.startsWith("server/$platform/") && !it.isDirectory }
                                .map { it.name }
                                .toList()
                            logger.info("Files in server/$platform/: ${entries.joinToString(", ")}")
                            jarFile.close()
                        } catch (e: Exception) {
                            logger.warn("Could not list JAR contents: ${e.message}")
                        }
                    }
                } else {
                    logger.warn("Server directory not found: $serverDirPath")
                }
            } catch (e: Exception) {
                logger.warn("Error inspecting server directory: ${e.message}")
            }

            val classLoader = this::class.java.classLoader
            val stream = classLoader.getResourceAsStream("server/$platform/$executableName")
                ?: this::class.java.getResourceAsStream(resourcePath)

            if (stream == null) {
                logger.warn("Bundled server not found at: $resourcePath")
                return null
            }

            val target = tmpDir.resolve(executableName)
            Files.copy(stream, target, StandardCopyOption.REPLACE_EXISTING)
            stream.close()

            if (!platform.contains("win")) {
                target.toFile().setExecutable(true)
            }

            logger.info("Extracted bundled server to: $target")

            if (!Files.exists(target) || Files.size(target) == 0L) {
                logger.error("Extracted file is empty or doesn't exist: $target")
                return null
            }

            val sizeMB = Files.size(target) / (1024.0 * 1024.0)
            logger.info("Server executable size: ${String.format("%.2f", sizeMB)} MB")

            return target.toAbsolutePath().toString()

        } catch (e: Exception) {
            logger.error("Failed to extract bundled server", e)
            return null
        }
    }

    private fun cleanup() {
        isRunning = false
        languageServer = null
        launcher = null
        process = null
    }

    private fun findServerPath(): String? {
        val settings = com.jetbrains.rider.plugins.sharpfocus.settings.SharpFocusSettings.getInstance(project)
        if (settings.serverPath.isNotEmpty()) {
            val customPath = settings.serverPath
            logger.info("Using custom server path from settings: $customPath")
            if (File(customPath).exists()) {
                return customPath
            } else {
                logger.warn("Custom server path does not exist: $customPath")
            }
        }

        val os = System.getProperty("os.name").lowercase()
        val arch = System.getProperty("os.arch").lowercase()
        val platform = when {
            os.contains("win") -> "win-x64"
            os.contains("mac") || os.contains("darwin") -> if (arch.contains("aarch64") || arch.contains("arm")) "osx-arm64" else "osx-x64"
            else -> "linux-x64"
        }
        val executableName = if (os.contains("win")) SERVER_EXE_NAME else SERVER_BINARY_NAME

        project.basePath?.let { basePath ->
            val devResourcePath = Paths.get(basePath, "src", "rider", "main", "resources", "server", platform, executableName).toString()
            logger.info("Checking development resources: $devResourcePath")
            if (File(devResourcePath).exists()) {
                logger.info("Found language server in development resources: $devResourcePath")
                return devResourcePath
            }

            val parentPath = File(basePath).parent
            if (parentPath != null) {
                val parentResourcePath = Paths.get(parentPath, "rider-plugin", "src", "rider", "main", "resources", "server", platform, executableName).toString()
                logger.info("Checking parent development resources: $parentResourcePath")
                if (File(parentResourcePath).exists()) {
                    logger.info("Found language server in parent development resources: $parentResourcePath")
                    return parentResourcePath
                }
            }
        }

        try {
            logger.info("Looking for bundled language server for platform: $platform")

            val extractedPath = extractBundledServer(platform)
            if (extractedPath != null) {
                logger.info("Successfully extracted bundled language server to: $extractedPath")
                return extractedPath
            } else {
                logger.warn("Bundled language server resources not found for platform: $platform")
            }
        } catch (e: Exception) {
            logger.warn("Error while attempting to extract bundled language server resource", e)
        }

        logger.error("Language server not found in any of the searched paths")
        return null
    }

    override fun dispose() {
        stop().get(10, TimeUnit.SECONDS)
    }
}
