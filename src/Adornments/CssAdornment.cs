﻿using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LessCompiler
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("CSS")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class CssAdornmentProvider : IWpfTextViewCreationListener
    {
        [Import]
        private ITextDocumentFactoryService DocumentService { get; set; }

        public void TextViewCreated(IWpfTextView textView)
        {
            if (!DocumentService.TryGetTextDocument(textView.TextBuffer, out ITextDocument doc))
                return;

            if (!Path.GetExtension(doc.FilePath).Equals(".css", StringComparison.OrdinalIgnoreCase))
                return;

            ThreadHelper.Generic.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                EnvDTE.Project project = VsHelpers.DTE.Solution.FindProjectItem(doc.FilePath)?.ContainingProject;

                if (project == null || !Settings.IsEnabled(project) || !LessCatalog.Catalog.ContainsKey(project.UniqueName))
                    return;

                ProjectMap map = LessCatalog.Catalog[project.UniqueName];

                bool isOutput = map.LessFiles.Keys.Any(l =>
                    l.OutputFilePath.Equals(doc.FilePath, StringComparison.OrdinalIgnoreCase) ||
                    (l.Minify && l.OutputFilePath.Equals(doc.FilePath.Replace(".min.css", ".css"), StringComparison.OrdinalIgnoreCase)));

                if (isOutput)
                    textView.Properties.GetOrCreateSingletonProperty(() => new CssAdornment(textView));
            });
        }
    }

    class CssAdornment : TextBlock
    {
        public CssAdornment(IWpfTextView view)
        {
            Visibility = Visibility.Hidden;

            Loaded += (s, e) =>
            {
                Initialize();
            };

            IAdornmentLayer adornmentLayer = view.GetAdornmentLayer(AdornmentLayer.LayerName);

            if (adornmentLayer.IsEmpty)
                adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, this, null);

            ThreadHelper.Generic.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                SetAdornmentLocation(view, EventArgs.Empty);

                view.ViewportHeightChanged += SetAdornmentLocation;
                view.ViewportWidthChanged += SetAdornmentLocation;
            });
        }

        private void Initialize()
        {
            Text = "Generated";
            FontSize = 75;
            Opacity = 0.4;
            FontWeight = FontWeights.Bold;
            Foreground = Brushes.Gray;
            ToolTip = "This file was generated by the LESS Compiler extension";
            SetValue(TextOptions.TextRenderingModeProperty, TextRenderingMode.Aliased);
            SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);
        }

        private void SetAdornmentLocation(object sender, EventArgs e)
        {
            var view = (IWpfTextView)sender;
            Canvas.SetLeft(this, view.ViewportRight - ActualWidth - 20);
            Canvas.SetTop(this, view.ViewportBottom - ActualHeight - 20);
            Visibility = Visibility.Visible;
        }
    }
}
