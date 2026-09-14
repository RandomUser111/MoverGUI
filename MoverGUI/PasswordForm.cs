namespace MoverGUI;

internal sealed class PasswordForm : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly List<(string Name, string Value)> _items;

    public IReadOnlyList<(string Name, string Value)> Passwords => _items;

    public PasswordForm(IEnumerable<(string Name, string Value)> current)
    {
        _items = current.ToList();
        Text = "Пароли";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(410, 290);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(_list, 0, 0);

        var tools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 6, 0, 0) };
        var add = new Button { Text = "Добавить", AutoSize = true };
        var edit = new Button { Text = "Изменить", AutoSize = true };
        var del = new Button { Text = "Удалить", AutoSize = true };
        var up = new Button { Text = "Выше", AutoSize = true };
        var down = new Button { Text = "Ниже", AutoSize = true };
        tools.Controls.AddRange([add, edit, del, up, down]);
        root.Controls.Add(tools, 0, 1);

        var bottom = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
        var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        bottom.Controls.AddRange([ok, cancel]);
        root.Controls.Add(bottom, 0, 2);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;

        add.Click += (_, _) => AddPassword();
        edit.Click += (_, _) => EditPassword();
        del.Click += (_, _) => DeletePassword();
        up.Click += (_, _) => Move(-1);
        down.Click += (_, _) => Move(1);
        _list.DoubleClick += (_, _) => EditPassword();

        RefreshList();
    }

    private void AddPassword()
    {
        using var dlg = new PasswordEditForm($"Пароль {_items.Count + 1}", "");
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _items.Add((dlg.PasswordName, dlg.PasswordValue));
        RefreshList(_items.Count - 1);
    }

    private void EditPassword()
    {
        if (_list.SelectedIndex < 0) return;
        var i = _list.SelectedIndex;
        using var dlg = new PasswordEditForm(_items[i].Name, _items[i].Value);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _items[i] = (dlg.PasswordName, dlg.PasswordValue);
        RefreshList(i);
    }

    private void DeletePassword()
    {
        if (_list.SelectedIndex < 0) return;
        var i = _list.SelectedIndex;
        _items.RemoveAt(i);
        RefreshList(Math.Min(i, _items.Count - 1));
    }

    private void Move(int delta)
    {
        var i = _list.SelectedIndex;
        if (i < 0) return;
        var j = i + delta;
        if (j < 0 || j >= _items.Count) return;
        (_items[i], _items[j]) = (_items[j], _items[i]);
        RefreshList(j);
    }

    private void RefreshList(int selected = -1)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in _items)
            _list.Items.Add($"{item.Name}    ••••••••");
        _list.EndUpdate();
        if (selected >= 0 && selected < _list.Items.Count)
            _list.SelectedIndex = selected;
    }
}

internal sealed class PasswordEditForm : Form
{
    private readonly TextBox _name = new();
    private readonly TextBox _value = new() { UseSystemPasswordChar = true };

    public string PasswordName => _name.Text.Trim();
    public string PasswordValue => _value.Text;

    public PasswordEditForm(string name, string value)
    {
        Text = "Пароль";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(390, 145);
        Font = new Font("Segoe UI", 9F);

        _name.Text = name;
        _value.Text = value;

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 2, RowCount = 3 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.Controls.Add(new Label { Text = "Название", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        table.Controls.Add(_name, 1, 0);
        table.Controls.Add(new Label { Text = "Пароль", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        table.Controls.Add(_value, 1, 1);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.AddRange([ok, cancel]);
        table.SetColumnSpan(buttons, 2);
        table.Controls.Add(buttons, 0, 2);
        Controls.Add(table);
        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_name.Text) || string.IsNullOrEmpty(_value.Text))
            {
                MessageBox.Show(this, "Укажите название и пароль.", "Mover GUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };
    }
}
