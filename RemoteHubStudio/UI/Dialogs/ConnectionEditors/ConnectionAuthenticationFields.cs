namespace RemoteHubStudio.UI.Dialogs.ConnectionEditors;

/// <summary>Describes which inline authentication values a concrete client mode consumes. / 描述具体客户端模式使用哪些内联认证值。</summary>
[Flags]
public enum ConnectionAuthenticationFields
{
    None = 0,
    Username = 1,
    Password = 2,
    UsernameAndPassword = Username | Password
}
