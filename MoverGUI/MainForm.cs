using System.ComponentModel;
using System.Diagnostics;
using System.Net;

namespace MoverGUI;

internal sealed class MainForm : Form
{
    private readonly ConfigStore _store = new();
    private readonly AppConfig _config;
    private readonly BindingList<CashRegister> _cashBinding;
    private readonly BindingList<string> _files = [];
    private readonly DataGridView _cashGrid = new();
    private readonly ListBox _fileList = new();
    private readonly TextBox _remotePath = new();
    private readonly TextBox _user = new();
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Width = 75 };
    private readonly Button _passwordsButton = new();
    private readonly Button _sendButton = new();
    private readonly Button _cancelButton = new();
    private readonly RichTextBox _log = new();
    private readonly Label _summary = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    public MainForm()
    {
        _config = _store.Load();
        _cashBinding = new BindingList<CashRegister>(_config.Cashes);

        Text = "Mover GUI — SetRetail";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(830, 560);
        Size = new Size(930, 660);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        LoadValues();
        LoadUploadFolder();
        UpdatePasswordButton();
        UpdateSummary();

        FormClosing += (_, _) => SaveConfigFromUi();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var connection = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 8, Margin = new Padding(0, 0, 0, 6) };
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        connection.Controls.Add(new Label { Text = "Путь на кассе", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 0) }, 0, 0);
        _remotePath.Dock = DockStyle.Fill;
        connection.Controls.Add(_remotePath, 1, 0);
        connection.Controls.Add(new Label { Text = "Логин", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 6, 6, 0) }, 2, 0);
        _user.Dock = DockStyle.Fill;
        connection.Controls.Add(_user, 3, 0);
        connection.Controls.Add(new Label { Text = "Порт", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 6, 6, 0) }, 4, 0);
        connection.Controls.Add(_port, 5, 0);
        _passwordsButton.AutoSize = true;
        _passwordsButton.Margin = new Padding(10, 0, 0, 0);
        _passwordsButton.Click += (_, _) => EditPasswords();
        connection.Controls.Add(_passwordsButton, 6, 0);
        var openLogs = new Button { Text = "Логи", AutoSize = true, Margin = new Padding(6, 0, 0, 0) };
        openLogs.Click += (_, _) => OpenLogsFolder();
        connection.Controls.Add(openLogs, 7, 0);
        root.Controls.Add(connection, 0, 0);

        ConfigureCashGrid();
        root.Controls.Add(_cashGrid, 0, 1);

        var cashTools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 5, 0, 5) };
        var addCash = new Button { Text = "Добавить кассу", AutoSize = true };
        var deleteCash = new Button { Text = "Удалить", AutoSize = true };
        var pasteIps = new Button { Text = "Вставить IP", AutoSize = true };
        var allOn = new Button { Text = "Выбрать все", AutoSize = true };
        var allOff = new Button { Text = "Снять все", AutoSize = true };
        addCash.Click += (_, _) => AddCash();
        deleteCash.Click += (_, _) => DeleteSelectedCashes();
        pasteIps.Click += (_, _) => PasteIps();
        allOn.Click += (_, _) => SetAllCashes(true);
        allOff.Click += (_, _) => SetAllCashes(false);
        cashTools.Controls.AddRange([addCash, deleteCash, pasteIps, allOn, allOff]);
        root.Controls.Add(cashTools, 0, 2);

        var fileGroup = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
        fileGroup.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fileGroup.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _fileList.Dock = DockStyle.Fill;
        _fileList.HorizontalScrollbar = true;
        _fileList.AllowDrop = true;
        _fileList.DragEnter += FileList_DragEnter;
        _fileList.DragDrop += FileList_DragDrop;
        fileGroup.Controls.Add(_fileList, 0, 0);

        var fileTools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 0) };
        var addFiles = new Button { Text = "Добавить файлы", AutoSize = true };
        var addFolder = new Button { Text = "Добавить папку", AutoSize = true };
        var deleteFile = new Button { Text = "Убрать", AutoSize = true };
        var clear = new Button { Text = "Очистить", AutoSize = true };
        addFiles.Click += (_, _) => AddFiles();
        addFolder.Click += (_, _) => AddFolder();
        deleteFile.Click += (_, _) => RemoveSelectedFiles();
        clear.Click += (_, _) => { _files.Clear(); RefreshFileList(); };
        fileTools.Controls.AddRange([addFiles, addFolder, deleteFile, clear]);
        fileGroup.Controls.Add(fileTools, 0, 1);
        root.Controls.Add(fileGroup, 0, 3);

        _log.Dock = DockStyle.Fill;
        _log.ReadOnly = true;
        _log.WordWrap = false;
        _log.Height = 95;
        _log.Font = new Font("Consolas", 8.5F);
        _log.Visible = false;

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, Margin = new Padding(0, 6, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _summary.AutoSize = true;
        _summary.Anchor = AnchorStyles.Left;
        bottom.Controls.Add(_summary, 0, 0);
        var toggleLog = new Button { Text = "Показать журнал", AutoSize = true };
        toggleLog.Click += (_, _) =>
        {
            _log.Visible = !_log.Visible;
            toggleLog.Text = _log.Visible ? "Скрыть журнал" : "Показать журнал";
            root.RowStyles[4].SizeType = _log.Visible ? SizeType.Absolute : SizeType.Absolute;
            root.RowStyles[4].Height = _log.Visible ? 105 : 0;
        };
        bottom.Controls.Add(toggleLog, 1, 0);
        _cancelButton.Text = "Отмена";
        _cancelButton.AutoSize = true;
        _cancelButton.Enabled = false;
        _cancelButton.Click += (_, _) => _cts?.Cancel();
        bottom.Controls.Add(_cancelButton, 2, 0);
        _sendButton.Text = "Отправить";
        _sendButton.AutoSize = true;
        _sendButton.Font = new Font(Font, FontStyle.Bold);
        _sendButton.Click += async (_, _) => await StartTransferAsync();
        bottom.Controls.Add(_sendButton, 3, 0);

        root.Controls.Add(_log, 0, 4);
        root.Controls.Add(bottom, 0, 5);
        Controls.Add(root);
    }

    private void ConfigureCashGrid()
    {
        _cashGrid.Dock = DockStyle.Fill;
        _cashGrid.AutoGenerateColumns = false;
        _cashGrid.AllowUserToAddRows = false;
        _cashGrid.AllowUserToDeleteRows = false;
        _cashGrid.AllowUserToResizeRows = false;
        _cashGrid.RowHeadersVisible = false;
        _cashGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _cashGrid.MultiSelect = true;
        _cashGrid.BackgroundColor = SystemColors.Window;
        _cashGrid.BorderStyle = BorderStyle.Fixed3D;
        _cashGrid.DataSource = _cashBinding;

        _cashGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(CashRegister.Enabled),
            HeaderText = "",
            Width = 34,
            MinimumWidth = 34
        });
        _cashGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CashRegister.Name),
            HeaderText = "Наименование",
            Width = 180
        });
        _cashGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CashRegister.Ip),
            HeaderText = "IP-адрес",
            Width = 150
        });
        _cashGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CashRegister.Status),
            HeaderText = "Статус",
            Width = 150,
            ReadOnly = true
        });
        _cashGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CashRegister.Details),
            HeaderText = "Результат",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        });

        _cashGrid.CellValueChanged += (_, _) => UpdateSummary();
        _cashGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_cashGrid.IsCurrentCellDirty)
                _cashGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
    }

    private void LoadValues()
    {
        _remotePath.Text = _config.RemotePath;
        _user.Text = _config.UserName;
        _port.Value = Math.Clamp(_config.Port, 1, 65535);
    }

    private void SaveConfigFromUi()
    {
        _cashGrid.EndEdit();
        _config.RemotePath = _remotePath.Text.Trim();
        _config.UserName = _user.Text.Trim();
        _config.Port = (int)_port.Value;
        _config.Cashes = _cashBinding.ToList();
        _store.Save(_config);
    }

    private void LoadUploadFolder()
    {
        var upload = Path.Combine(AppContext.BaseDirectory, "Upload");
        if (!Directory.Exists(upload)) return;
        AddFilePaths(Directory.GetFiles(upload));
    }

    private void EditPasswords()
    {
        var current = new List<(string Name, string Value)>();
        foreach (var saved in _config.Passwords)
        {
            try { current.Add((saved.Name, Dpapi.Unprotect(saved.ProtectedValue))); }
            catch { }
        }

        using var dlg = new PasswordForm(current);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _config.Passwords = dlg.Passwords.Select(p => new SavedPassword
        {
            Name = p.Name,
            ProtectedValue = Dpapi.Protect(p.Value)
        }).ToList();

        foreach (var cash in _cashBinding)
            if (cash.PreferredPasswordIndex is >= 0 && cash.PreferredPasswordIndex >= _config.Passwords.Count)
                cash.PreferredPasswordIndex = null;

        _store.Save(_config);
        UpdatePasswordButton();
    }

    private void UpdatePasswordButton() => _passwordsButton.Text = $"Пароли ({_config.Passwords.Count})";

    private void AddCash()
    {
        var n = _cashBinding.Count + 1;
        _cashBinding.Add(new CashRegister { Name = $"Касса {n}", Enabled = true, Status = "Ожидание" });
        _cashGrid.CurrentCell = _cashGrid.Rows[^1].Cells[1];
        _cashGrid.BeginEdit(true);
        UpdateSummary();
    }

    private void DeleteSelectedCashes()
    {
        var items = _cashGrid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem as CashRegister)
            .Where(x => x is not null)
            .Cast<CashRegister>()
            .ToList();
        foreach (var item in items)
            _cashBinding.Remove(item);
        UpdateSummary();
    }

    private void PasteIps()
    {
        if (!Clipboard.ContainsText()) return;
        var text = Clipboard.GetText();
        var tokens = text.Split([' ', '\t', '\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var existing = _cashBinding.Select(x => x.Ip).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            if (!IPAddress.TryParse(token, out _)) continue;
            if (!existing.Add(token)) continue;
            _cashBinding.Add(new CashRegister { Name = $"Касса {_cashBinding.Count + 1}", Ip = token, Enabled = true });
        }
        UpdateSummary();
    }

    private void SetAllCashes(bool enabled)
    {
        foreach (var cash in _cashBinding) cash.Enabled = enabled;
        _cashGrid.Refresh();
        UpdateSummary();
    }

    private void AddFiles()
    {
        using var dlg = new OpenFileDialog { Multiselect = true, Title = "Выберите файлы для отправки" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            AddFilePaths(dlg.FileNames);
    }

    private void AddFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Выберите папку. Будут добавлены файлы из нее без вложенных подпапок." };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            AddFilePaths(Directory.GetFiles(dlg.SelectedPath));
    }

    private void AddFilePaths(IEnumerable<string> paths)
    {
        var existing = _files.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Where(File.Exists))
            if (existing.Add(path))
                _files.Add(path);
        RefreshFileList();
    }

    private void RefreshFileList()
    {
        _fileList.BeginUpdate();
        _fileList.Items.Clear();
        foreach (var file in _files)
        {
            var info = new FileInfo(file);
            _fileList.Items.Add($"{info.Name}    {FormatSize(info.Length)}    {info.DirectoryName}");
        }
        _fileList.EndUpdate();
        UpdateSummary();
    }

    private void RemoveSelectedFiles()
    {
        var indices = _fileList.SelectedIndices.Cast<int>().OrderByDescending(x => x).ToList();
        foreach (var i in indices)
            _files.RemoveAt(i);
        RefreshFileList();
    }

    private void FileList_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void FileList_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var files = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path)) files.Add(path);
            else if (Directory.Exists(path)) files.AddRange(Directory.GetFiles(path));
        }
        AddFilePaths(files);
    }

    private async Task StartTransferAsync()
    {
        if (_busy) return;
        _cashGrid.EndEdit();
        SaveConfigFromUi();

        var targets = _cashBinding.Where(c => c.Enabled && !string.IsNullOrWhiteSpace(c.Ip)).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Не выбраны кассы для отправки.", "Mover GUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_files.Count == 0)
        {
            MessageBox.Show(this, "Не добавлены файлы для отправки.", "Mover GUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_config.Passwords.Count == 0)
        {
            MessageBox.Show(this, "Добавьте хотя бы один пароль.", "Mover GUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_remotePath.Text) || string.IsNullOrWhiteSpace(_user.Text))
        {
            MessageBox.Show(this, "Укажите путь на кассе и логин.", "Mover GUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var passwords = new List<string>();
        foreach (var saved in _config.Passwords)
        {
            try { passwords.Add(Dpapi.Unprotect(saved.ProtectedValue)); }
            catch { passwords.Add(""); }
        }
        if (passwords.All(string.IsNullOrEmpty))
        {
            MessageBox.Show(this, "Не удалось расшифровать сохраненные пароли. Откройте список паролей и сохраните их заново.", "Mover GUI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SetBusy(true);
        _cts = new CancellationTokenSource();
        _log.Clear();
        var service = new PscpService(_store.DataDirectory);
        var semaphore = new SemaphoreSlim(Math.Clamp(_config.MaxParallel, 1, 16));
        var results = new List<TransferResult>();
        var sync = new object();

        foreach (var cash in targets)
        {
            cash.Status = "В очереди";
            cash.Details = "";
        }
        _cashGrid.Refresh();

        var tasks = targets.Select(async cash =>
        {
            await semaphore.WaitAsync(_cts.Token);
            try
            {
                Invoke(() =>
                {
                    cash.Status = "Подключение";
                    _cashGrid.Refresh();
                });

                var progress = new Progress<string>(AppendLog);
                var result = await service.SendAsync(cash, _files.ToList(), _remotePath.Text.Trim(), _user.Text.Trim(), (int)_port.Value, passwords, progress, _cts.Token);

                lock (sync) results.Add(result);
                Invoke(() =>
                {
                    cash.Status = result.Status;
                    cash.Details = result.Details;
                    if (result.WorkingPasswordIndex.HasValue)
                        cash.PreferredPasswordIndex = result.WorkingPasswordIndex;
                    _cashGrid.Refresh();
                    UpdateSummary();
                });
            }
            catch (OperationCanceledException)
            {
                Invoke(() =>
                {
                    cash.Status = "Отменено";
                    cash.Details = "Операция отменена пользователем";
                    _cashGrid.Refresh();
                });
            }
            catch (Exception ex)
            {
                Invoke(() =>
                {
                    cash.Status = "Ошибка";
                    cash.Details = ex.Message;
                    _cashGrid.Refresh();
                });
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException) { }
        finally
        {
            WriteTransferLog(targets);
            SaveConfigFromUi();
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
            UpdateSummary();
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _sendButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        _remotePath.Enabled = !busy;
        _user.Enabled = !busy;
        _port.Enabled = !busy;
        _passwordsButton.Enabled = !busy;
        _cashGrid.ReadOnly = busy;
    }

    private void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(text));
            return;
        }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void WriteTransferLog(IEnumerable<CashRegister> cashes)
    {
        try
        {
            Directory.CreateDirectory(_store.LogsDirectory);
            var path = Path.Combine(_store.LogsDirectory, $"transfer-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
            using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            writer.WriteLine($"Mover GUI {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Remote: {_user.Text}@*:{_remotePath.Text} port {_port.Value}");
            writer.WriteLine($"Files: {_files.Count}");
            foreach (var file in _files) writer.WriteLine($"  {file}");
            writer.WriteLine();
            foreach (var cash in cashes)
                writer.WriteLine($"{cash.Ip}\t{cash.Name}\t{cash.Status}\t{cash.Details}");
        }
        catch { }
    }

    private void UpdateSummary()
    {
        var selected = _cashBinding.Count(c => c.Enabled);
        var done = _cashBinding.Count(c => c.Status == "Готово");
        var errors = _cashBinding.Count(c => c.Status is "Ошибка" or "Ошибка копирования" or "Нет связи" or "Пароль не подошел");
        _summary.Text = $"Касс: {selected}    Файлов: {_files.Count}    Готово: {done}    Ошибок: {errors}";
    }

    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(_store.LogsDirectory);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _store.LogsDirectory, UseShellExecute = true });
        }
        catch { }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.#} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024:0.#} MB";
        return $"{bytes / 1024d / 1024 / 1024:0.##} GB";
    }
}
