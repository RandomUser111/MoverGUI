using System.Text.Json.Serialization;

namespace MoverGUI;

public sealed class AppConfig
{
    public string RemotePath { get; set; } = "/home/tc/storage/";
    public string UserName { get; set; } = "tc";
    public int Port { get; set; } = 22;
    public int MaxParallel { get; set; } = 4;
    public List<CashRegister> Cashes { get; set; } = [];
    public List<SavedPassword> Passwords { get; set; } = [];
}

public sealed class CashRegister
{
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int? PreferredPasswordIndex { get; set; }

    [JsonIgnore]
    public string Status { get; set; } = "Ожидание";

    [JsonIgnore]
    public string Details { get; set; } = "";
}

public sealed class SavedPassword
{
    public string Name { get; set; } = "Пароль";
    public string ProtectedValue { get; set; } = "";
}

public sealed class TransferResult
{
    public required CashRegister Cash { get; init; }
    public bool Success { get; init; }
    public string Status { get; init; } = "";
    public string Details { get; init; } = "";
    public int? WorkingPasswordIndex { get; init; }
}
