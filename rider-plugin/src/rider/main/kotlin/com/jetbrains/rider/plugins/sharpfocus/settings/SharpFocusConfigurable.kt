package com.jetbrains.rider.plugins.sharpfocus.settings

import com.intellij.openapi.fileChooser.FileChooser
import com.intellij.openapi.fileChooser.FileChooserDescriptorFactory
import com.intellij.openapi.options.Configurable
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.ComboBox
import com.intellij.openapi.ui.TextFieldWithBrowseButton
import com.intellij.ui.components.JBCheckBox
import com.intellij.ui.components.JBLabel
import com.intellij.util.ui.FormBuilder
import com.intellij.util.ui.JBUI
import com.jetbrains.rider.plugins.sharpfocus.highlighting.DisplayMode
import java.awt.BorderLayout
import javax.swing.*

/**
 * Settings UI for SharpFocus plugin.
 * Provides configuration interface in Rider's Settings dialog.
 */
class SharpFocusConfigurable(private val project: Project) : Configurable {

    private var settingsPanel: JPanel? = null
    private var analysisModeComboBox: ComboBox<AnalysisMode>? = null
    private var displayModeComboBox: ComboBox<DisplayMode>? = null
    private var serverPathField: TextFieldWithBrowseButton? = null
    private var enableTracingCheckBox: JBCheckBox? = null
    private var disableWordHighlightCheckBox: JBCheckBox? = null

    override fun getDisplayName(): String = "SharpFocus"

    override fun createComponent(): JComponent {
        val settings = SharpFocusSettings.getInstance(project)

        // Analysis Mode
        analysisModeComboBox = ComboBox(AnalysisMode.values()).apply {
            renderer = object : DefaultListCellRenderer() {
                override fun getListCellRendererComponent(
                    list: JList<*>?,
                    value: Any?,
                    index: Int,
                    isSelected: Boolean,
                    cellHasFocus: Boolean
                ): java.awt.Component {
                    val component = super.getListCellRendererComponent(list, value, index, isSelected, cellHasFocus)
                    if (value is AnalysisMode) {
                        text = value.displayName
                    }
                    return component
                }
            }
            selectedItem = settings.analysisMode
        }

        val analysisModeLabel = JBLabel("Analysis Mode:")
        val analysisModeDescription = JBLabel(
            "<html><i>Choose when SharpFocus analyzes your code</i></html>"
        ).apply {
            foreground = JBUI.CurrentTheme.ContextHelp.FOREGROUND
        }

        // Display Mode
        displayModeComboBox = ComboBox(DisplayMode.values()).apply {
            renderer = object : DefaultListCellRenderer() {
                override fun getListCellRendererComponent(
                    list: JList<*>?,
                    value: Any?,
                    index: Int,
                    isSelected: Boolean,
                    cellHasFocus: Boolean
                ): java.awt.Component {
                    val component = super.getListCellRendererComponent(list, value, index, isSelected, cellHasFocus)
                    if (value is DisplayMode) {
                        text = when (value) {
                            DisplayMode.NORMAL -> "Normal (Minimal, faded style)"
                            DisplayMode.ADVANCED -> "Advanced (Color-coded relations)"
                        }
                    }
                    return component
                }
            }
            selectedItem = settings.displayMode
        }

        val displayModeLabel = JBLabel("Display Mode:")
        val displayModeDescription = JBLabel(
            "<html><i>Choose how flow locations are rendered in the editor</i></html>"
        ).apply {
            foreground = JBUI.CurrentTheme.ContextHelp.FOREGROUND
        }

        // Server Path
        serverPathField = TextFieldWithBrowseButton().apply {
            text = settings.serverPath
            addActionListener {
                val descriptor = FileChooserDescriptorFactory.createSingleFileDescriptor("dll")
                val file = FileChooser.chooseFile(descriptor, project, null)
                file?.let {
                    this.text = it.path
                }
            }
        }

        val serverPathLabel = JBLabel("Language Server Path:")
        val serverPathDescription = JBLabel(
            "<html><i>Path to SharpFocus.LanguageServer.dll (leave empty for auto-detection)</i></html>"
        ).apply {
            foreground = JBUI.CurrentTheme.ContextHelp.FOREGROUND
        }

        // Enable Tracing
        enableTracingCheckBox = JBCheckBox("Enable language server tracing").apply {
            isSelected = settings.enableTracing
        }

        val tracingDescription = JBLabel(
            "<html><i>Log communication between plugin and language server (for debugging)</i></html>"
        ).apply {
            foreground = JBUI.CurrentTheme.ContextHelp.FOREGROUND
        }

        // Disable Native Word Highlight
        disableWordHighlightCheckBox = JBCheckBox("Disable native word highlighting").apply {
            isSelected = settings.disableNativeWordHighlight
        }

        val wordHighlightDescription = JBLabel(
            "<html><i>Prevent conflicts with Rider's built-in word highlighting (recommended)</i></html>"
        ).apply {
            foreground = JBUI.CurrentTheme.ContextHelp.FOREGROUND
        }

        // Build form
        val formBuilder = FormBuilder.createFormBuilder()
            .addLabeledComponent(analysisModeLabel, analysisModeComboBox!!)
            .addComponentToRightColumn(analysisModeDescription)
            .addVerticalGap(8)
            .addLabeledComponent(displayModeLabel, displayModeComboBox!!)
            .addComponentToRightColumn(displayModeDescription)
            .addVerticalGap(8)
            .addLabeledComponent(serverPathLabel, serverPathField!!)
            .addComponentToRightColumn(serverPathDescription)
            .addVerticalGap(8)
            .addComponent(enableTracingCheckBox!!)
            .addComponentToRightColumn(tracingDescription)
            .addVerticalGap(8)
            .addComponent(disableWordHighlightCheckBox!!)
            .addComponentToRightColumn(wordHighlightDescription)
            .addComponentFillVertically(JPanel(), 0)

        settingsPanel = JPanel(BorderLayout()).apply {
            add(formBuilder.panel, BorderLayout.NORTH)
            border = JBUI.Borders.empty(10)
        }

        return settingsPanel!!
    }

    override fun isModified(): Boolean {
        val settings = SharpFocusSettings.getInstance(project)

        return analysisModeComboBox?.selectedItem != settings.analysisMode ||
                displayModeComboBox?.selectedItem != settings.displayMode ||
                serverPathField?.text != settings.serverPath ||
                enableTracingCheckBox?.isSelected != settings.enableTracing ||
                disableWordHighlightCheckBox?.isSelected != settings.disableNativeWordHighlight
    }

    override fun apply() {
        val settings = SharpFocusSettings.getInstance(project)

        settings.analysisMode = analysisModeComboBox?.selectedItem as? AnalysisMode ?: AnalysisMode.FOCUS
        settings.displayMode = displayModeComboBox?.selectedItem as? DisplayMode ?: DisplayMode.NORMAL
        settings.serverPath = serverPathField?.text ?: ""
        settings.enableTracing = enableTracingCheckBox?.isSelected ?: false
        settings.disableNativeWordHighlight = disableWordHighlightCheckBox?.isSelected ?: true

        // Update identifier highlighting based on new settings
        com.jetbrains.rider.plugins.sharpfocus.SharpFocusPlugin.getInstance(project)
            .updateIdentifierHighlighting()
    }

    override fun reset() {
        val settings = SharpFocusSettings.getInstance(project)

        analysisModeComboBox?.selectedItem = settings.analysisMode
        displayModeComboBox?.selectedItem = settings.displayMode
        serverPathField?.text = settings.serverPath
        enableTracingCheckBox?.isSelected = settings.enableTracing
        disableWordHighlightCheckBox?.isSelected = settings.disableNativeWordHighlight
    }

    override fun disposeUIResources() {
        settingsPanel = null
        analysisModeComboBox = null
        displayModeComboBox = null
        serverPathField = null
        enableTracingCheckBox = null
        disableWordHighlightCheckBox = null
    }
}
