package com.jetbrains.rider.plugins.sharpfocus.codevision

import com.intellij.codeInsight.codeVision.*
import com.intellij.codeInsight.codeVision.ui.model.ClickableTextCodeVisionEntry
import com.intellij.codeInsight.codeVision.ui.model.TextCodeVisionEntry
import com.intellij.openapi.editor.Editor
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.TextRange
import com.intellij.psi.PsiFile
import com.jetbrains.rider.plugins.sharpfocus.lsp.FocusModeResponse
import com.jetbrains.rider.plugins.sharpfocus.lsp.SliceRelation
import com.jetbrains.rider.plugins.sharpfocus.toolwindow.FlowTreeToolWindowFactory
import java.awt.event.MouseEvent

/**
 * Provides CodeVision hints for focused symbols showing data flow statistics.
 * Similar to VS Code's CodeLens feature.
 */
class SharpFocusCodeVisionProvider : CodeVisionProvider<Unit> {

    companion object {
        const val ID = "SharpFocus.DataFlow"

        @Volatile
        private var currentResponse: FocusModeResponse? = null
        @Volatile
        private var currentFilePath: String? = null

        /**
         * Update the current focus mode response.
         */
        fun updateResponse(filePath: String?, response: FocusModeResponse?) {
            currentFilePath = filePath
            currentResponse = response
        }

        /**
         * Clear the current response.
         */
        fun clear() {
            currentFilePath = null
            currentResponse = null
        }
    }

    override val defaultAnchor: CodeVisionAnchorKind
        get() = CodeVisionAnchorKind.Top

    override val id: String
        get() = ID

    override val name: String
        get() = "SharpFocus Data Flow"

    override val relativeOrderings: List<CodeVisionRelativeOrdering>
        get() = emptyList()

    override fun precomputeOnUiThread(editor: Editor) {}

    override fun computeCodeVision(editor: Editor, uiData: Unit): CodeVisionState {
        val file = editor.virtualFile ?: return CodeVisionState.READY_EMPTY
        val filePath = file.path

        // Check if we have a response for this file
        if (currentFilePath != filePath || currentResponse == null) {
            return CodeVisionState.READY_EMPTY
        }

        val response = currentResponse ?: return CodeVisionState.READY_EMPTY
        val project = editor.project ?: return CodeVisionState.READY_EMPTY

        val entries = mutableListOf<Pair<TextRange, CodeVisionEntry>>()

        // Create code vision entry for the focused symbol (seed)
        val range = response.focusedPlace.range
        val startOffset = getOffset(editor, range.start.line, range.start.character)
        val endOffset = getOffset(editor, range.end.line, range.end.character)

        if (startOffset >= 0 && endOffset >= 0 && startOffset < endOffset) {
            val counts = countRelations(response)
            val text = generateCodeVisionText(
                response.focusedPlace.name,
                counts.first,
                counts.second,
                counts.third
            )

            val entry: CodeVisionEntry = ClickableTextCodeVisionEntry(
                text,
                ID,
                onClick = { event, _ ->
                    onCodeVisionClick(project, response, event)
                },
                longPresentation = generateTooltip(
                    response.focusedPlace.name,
                    counts.first,
                    counts.second,
                    counts.third
                )
            )

            val textRange = TextRange(startOffset, endOffset)
            entries.add(Pair(textRange, entry))
        }

        // Add code vision entries for each slice detail
        val focusedSymbol = response.focusedPlace.name
        val processedLines = mutableSetOf<Int>()

        // Process backward slice (what influences the seed)
        response.backwardSlice?.sliceRangeDetails?.forEach { detail ->
            val line = detail.place.range.start.line
            if (line !in processedLines && line != range.start.line) {
                processedLines.add(line)

                val detailStartOffset = getOffset(editor, detail.place.range.start.line, detail.place.range.start.character)
                val detailEndOffset = getOffset(editor, detail.place.range.end.line, detail.place.range.end.character)

                if (detailStartOffset >= 0 && detailEndOffset >= 0 && detailStartOffset < detailEndOffset) {
                    val text = "influences $focusedSymbol"
                    val entry: CodeVisionEntry = TextCodeVisionEntry(
                        text,
                        ID,
                        tooltip = "This code influences $focusedSymbol"
                    )
                    entries.add(Pair(TextRange(detailStartOffset, detailEndOffset), entry))
                }
            }
        }

        // Process forward slice (what is influenced by the seed)
        response.forwardSlice?.sliceRangeDetails?.forEach { detail ->
            val line = detail.place.range.start.line
            if (line !in processedLines && line != range.start.line) {
                processedLines.add(line)

                val detailStartOffset = getOffset(editor, detail.place.range.start.line, detail.place.range.start.character)
                val detailEndOffset = getOffset(editor, detail.place.range.end.line, detail.place.range.end.character)

                if (detailStartOffset >= 0 && detailEndOffset >= 0 && detailStartOffset < detailEndOffset) {
                    val text = "influenced by $focusedSymbol"
                    val entry: CodeVisionEntry = TextCodeVisionEntry(
                        text,
                        ID,
                        tooltip = "$focusedSymbol influences this code"
                    )
                    entries.add(Pair(TextRange(detailStartOffset, detailEndOffset), entry))
                }
            }
        }

        return if (entries.isEmpty()) {
            CodeVisionState.READY_EMPTY
        } else {
            CodeVisionState.Ready(entries)
        }
    }

    private fun getOffset(editor: Editor, line: Int, character: Int): Int {
        val document = editor.document
        if (line < 0 || line >= document.lineCount) {
            return -1
        }
        val lineStartOffset = document.getLineStartOffset(line)
        return lineStartOffset + character
    }

    private fun countRelations(response: FocusModeResponse): Triple<Int, Int, Int> {
        val backwardCount = response.backwardSlice?.sliceRangeDetails?.size ?: 0
        val forwardCount = response.forwardSlice?.sliceRangeDetails?.size ?: 0

        // Count transforms (items that appear in both slices)
        val backwardLines = response.backwardSlice?.sliceRangeDetails?.map { it.place.range.start.line }?.toSet() ?: emptySet()
        val forwardLines = response.forwardSlice?.sliceRangeDetails?.map { it.place.range.start.line }?.toSet() ?: emptySet()
        val transformCount = backwardLines.intersect(forwardLines).size

        return Triple(backwardCount, forwardCount, transformCount)
    }

    private fun generateCodeVisionText(
        symbolName: String,
        sourceCount: Int,
        sinkCount: Int,
        transformCount: Int
    ): String {
        val totalCount = sourceCount + sinkCount + transformCount

        if (totalCount == 0) {
            return "$symbolName · isolated · no data flow detected"
        }

        if (sourceCount > 0 && sinkCount == 0 && transformCount == 0) {
            return "$symbolName · influenced by $sourceCount · not used"
        }

        if (sourceCount == 0 && sinkCount > 0 && transformCount == 0) {
            return "$symbolName · influences $sinkCount ${pluralize("location", sinkCount)}"
        }

        if (sourceCount == 0 && sinkCount == 0 && transformCount > 0) {
            return "$symbolName · $transformCount ${pluralize("transform", transformCount)}"
        }

        val parts = mutableListOf<String>()
        if (sourceCount > 0) {
            parts.add("influenced by $sourceCount")
        }
        if (sinkCount > 0) {
            parts.add("influences $sinkCount")
        }
        if (transformCount > 0 && parts.isEmpty()) {
            // Fallback: if only transforms exist with sources/sinks but parts is still empty
            parts.add("$transformCount ${pluralize("transform", transformCount)}")
        }

        return "$symbolName · ${parts.joinToString(" · ")}  [Show Flow]"
    }

    private fun generateTooltip(
        symbolName: String,
        sourceCount: Int,
        sinkCount: Int,
        transformCount: Int
    ): String {
        val parts = mutableListOf("Data flow for '$symbolName'")

        if (sourceCount > 0) {
            parts.add("• $sourceCount ${pluralize("location", sourceCount)} (what influences it)")
        }
        if (transformCount > 0) {
            parts.add("• $transformCount ${pluralize("transform", transformCount)}")
        }
        if (sinkCount > 0) {
            parts.add("• $sinkCount ${pluralize("location", sinkCount)} (what it influences)")
        }

        if (sourceCount == 0 && sinkCount == 0 && transformCount == 0) {
            parts.add("No data flow connections detected")
        }

        parts.add("\nClick to view details in tool window")

        return parts.joinToString("\n")
    }

    private fun pluralize(word: String, count: Int): String {
        return if (count == 1) word else "${word}s"
    }

    private fun onCodeVisionClick(project: Project, response: FocusModeResponse, event: MouseEvent?) {
        // Show the flow tree tool window when clicked
        val toolWindowManager = com.intellij.openapi.wm.ToolWindowManager.getInstance(project)
        val toolWindow = toolWindowManager.getToolWindow("SharpFocus Flow")

        toolWindow?.show {
            // Tool window is now visible
        }
    }
}
