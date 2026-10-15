using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace first
{
    public partial class Form1 : Form
    {
        private RichTextBox _editor;
        private TextBox _results;
        private Label _status;
        private string _currentFile;
        private bool _loadingText;
        private bool _runtimeInterfaceInitialized;

        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_runtimeInterfaceInitialized || IsInDesignMode())
                return;

            _runtimeInterfaceInitialized = true;
            BuildInterface();
            LoadExample();
        }

        private bool IsInDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime ||
                (Site != null && Site.DesignMode);
        }

        private void BuildInterface()
        {
            // Старые элементы из Designer не должны перекрывать новую форму.
            Controls.Clear();
            AcceptButton = null; // Enter остаётся переводом строки в редакторе.
            CancelButton = null;
            Text = "Анализатор F# — метрика Джилба";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 650);
            MinimumSize = new Size(760, 480);
            Font = new Font("Segoe UI", 9F);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            Controls.Add(root);

            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(5);
            toolbar.Margin = Padding.Empty;
            toolbar.Controls.Add(CreateButton("Пример", delegate { LoadExample(); }, 85));
            toolbar.Controls.Add(CreateButton("Открыть…", OpenFile, 100));
            toolbar.Controls.Add(CreateButton("Сохранить", SaveFile, 100));
            toolbar.Controls.Add(CreateButton("Рассчитать (F5)", delegate { RunAnalysis(); }, 145));
            root.Controls.Add(toolbar, 0, 0);

            SplitContainer split = new SplitContainer();
            split.Size = new Size(980, 560);
            split.Dock = DockStyle.Fill;
            split.Margin = new Padding(8, 0, 8, 4);
            split.Panel1MinSize = 300;
            split.Panel2MinSize = 250;
            split.SplitterDistance = 600;
            root.Controls.Add(split, 0, 1);

            TableLayoutPanel editorPanel = CreatePanel("Исходный код F#", split.Panel1);
            _editor = new RichTextBox();
            _editor.Dock = DockStyle.Fill;
            _editor.Font = new Font("Consolas", 10F);
            _editor.WordWrap = false;
            _editor.AcceptsTab = true;
            _editor.DetectUrls = false;
            _editor.HideSelection = false;
            _editor.TextChanged += delegate
            {
                if (!_loadingText)
                    SetStatus("Код изменён — нажмите F5 для обновления результата");
            };
            _editor.KeyDown += EditorKeyDown;
            editorPanel.Controls.Add(_editor, 0, 1);

            TableLayoutPanel resultsPanel = CreatePanel("Результаты", split.Panel2);
            _results = new TextBox();
            _results.Dock = DockStyle.Fill;
            _results.Multiline = true;
            _results.ReadOnly = true;
            _results.WordWrap = true;
            _results.ScrollBars = ScrollBars.Vertical;
            _results.BackColor = SystemColors.Window;
            _results.Font = new Font("Segoe UI", 10F);
            resultsPanel.Controls.Add(_results, 0, 1);

            _status = new Label();
            _status.Dock = DockStyle.Fill;
            _status.AutoEllipsis = true;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Padding = new Padding(8, 0, 8, 0);
            root.Controls.Add(_status, 0, 2);
        }

        private static TableLayoutPanel CreatePanel(string title, Control parent)
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 2;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            parent.Controls.Add(panel);

            Label heading = new Label();
            heading.Text = title;
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            panel.Controls.Add(heading, 0, 0);
            return panel;
        }

        private static Button CreateButton(string text, EventHandler action, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 30;
            button.Click += action;
            return button;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_editor != null && (keyData == Keys.F5 || keyData == (Keys.Control | Keys.Enter)))
            {
                RunAnalysis();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            // F# использует отступы: вместо Tab вставляем четыре пробела.
            if (e.KeyCode == Keys.Tab && e.Modifiers == Keys.None)
            {
                _editor.SelectedText = "    ";
                e.SuppressKeyPress = true;
            }
        }

        private void LoadExample()
        {
            const string example =
                "let classify x =\r\n" +
                "    match x with\r\n" +
                "    | 0 -> 0\r\n" +
                "    | 1 -> 1\r\n" +
                "    | _ -> if x > 0 then 2 else -1\r\n" +
                "\r\n" +
                "for i in 0 .. 5 do\r\n" +
                "    printfn \"%d\" (classify i)\r\n";

            _currentFile = null;
            SetEditorText(example);
            UpdateTitle("пример");
            RunAnalysis();
        }

        private void OpenFile(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Открыть код F#";
                dialog.Filter = "Файлы F# (*.fs;*.fsx;*.fsi)|*.fs;*.fsx;*.fsi|Все файлы (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    string text;
                    using (StreamReader reader = new StreamReader(dialog.FileName, true))
                        text = reader.ReadToEnd();

                    // Меняем текущий путь только после успешного чтения.
                    _currentFile = dialog.FileName;
                    SetEditorText(text);
                    UpdateTitle(Path.GetFileName(_currentFile));
                    RunAnalysis();
                }
                catch (Exception ex)
                {
                    ShowError("Не удалось открыть файл", ex);
                }
            }
        }

        private void SaveFile(object sender, EventArgs e)
        {
            string path = _currentFile;
            if (String.IsNullOrEmpty(path) || !File.Exists(path))
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = "Сохранить код F#";
                    dialog.Filter = "Код F# (*.fs)|*.fs|Скрипт F# (*.fsx)|*.fsx|Все файлы (*.*)|*.*";
                    dialog.DefaultExt = "fs";
                    dialog.AddExtension = true;
                    dialog.OverwritePrompt = true;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    path = dialog.FileName;
                }
            }

            try
            {
                File.WriteAllText(path, _editor.Text, new UTF8Encoding(false));
                _currentFile = path;
                UpdateTitle(Path.GetFileName(path));
                SetStatus("Файл сохранён");
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сохранить файл", ex);
            }
        }

        private void RunAnalysis()
        {
            if (_editor == null || _results == null)
                return;

            try
            {
                AnalysisResult result = FSharpAnalyzer.Analyze(_editor.Text);
                CultureInfo culture = CultureInfo.CurrentCulture;
                StringBuilder text = new StringBuilder();

                text.AppendLine("МЕТРИКА ДЖИЛБА");
                text.AppendLine();
                text.AppendLine("CL (абсолютная): " + result.AbsoluteComplexity);
                text.AppendLine("cl (относительная): " + result.RelativeComplexity.ToString("0.000", culture));
                text.AppendLine("CLI (вложенность): " + result.MaxNesting);
                text.AppendLine();
                text.AppendLine("CL = if + циклы + ветки match");
                text.AppendLine("cl = CL / N");
                text.AppendLine();
                text.AppendLine("if / elif: " + result.IfCount);
                text.AppendLine("for / while: " + result.LoopCount);
                text.AppendLine("Ветки без безусловной _: " + result.SwitchCaseCount);
                text.AppendLine("Всего операторов N: " + result.TotalStatements);

                _results.Text = text.ToString();
                SetStatus("Расчёт завершён · N = " + result.TotalStatements +
                    " · замечаний: " + result.Warnings.Count);
            }
            catch (Exception ex)
            {
                _results.Clear();
                ShowError("Не удалось выполнить анализ", ex);
            }
        }

        private void SetEditorText(string text)
        {
            _loadingText = true;
            try
            {
                _editor.Text = text ?? String.Empty;
                _editor.SelectionStart = 0;
                _editor.SelectionLength = 0;
            }
            finally
            {
                _loadingText = false;
            }
        }

        private void UpdateTitle(string fileName)
        {
            Text = "Анализатор F#";
        }

        private void SetStatus(string text)
        {
            if (_status != null)
                _status.Text = text;
        }

        private void ShowError(string message, Exception ex)
        {
            SetStatus(message);
            MessageBox.Show(this, message + ".\r\n" + ex.Message,
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
