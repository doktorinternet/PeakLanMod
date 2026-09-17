using PeakLanMod.Lan.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakLanMod.Lan.UI;

// Owns the host-side "Require Password" toggle and conditional password field:
// their widgets and their state. Kept as its own collaborator instead of more
// inline code in LanOverlayController, per docs/research/password-protected-rooms-plan.md.
// Widget construction still borrows LanOverlayController's factory methods until the
// broader extraction in docs/research/lan-overlay-ui-refactor-plan.md lands.
internal sealed class HostPasswordFieldController
{
    private readonly ILanPluginOptions _options;
    private string _password = string.Empty;

    internal HostPasswordFieldController(ILanPluginOptions options)
    {
        _options = options;
    }

    internal Toggle? RequirePasswordToggle { get; private set; }
    internal TMP_Text? RequirePasswordToggleLabel { get; private set; }
    internal TMP_Text? PasswordLabel { get; private set; }
    internal InputField? PasswordInput { get; private set; }

    internal bool RequirePassword => _options.RequirePasswordForHostedRoom.Value;

    // In-memory only for the current session: never persisted to config, never logged.
    internal string Password => _password;

    internal void ClearPassword()
    {
        _password = string.Empty;

        if (PasswordInput != null)
        {
            PasswordInput.text = string.Empty;
        }
    }

    internal void EnsureUi(
        Transform parent,
        LanOverlayController widgetFactory)
    {
        if (RequirePasswordToggle != null)
        {
            return;
        }

        (Toggle toggle, TMP_Text toggleLabel) = widgetFactory.CreateToggle(
            "RequirePasswordToggle",
            parent,
            "Require Password",
            _options.RequirePasswordForHostedRoom.Value,
            OnRequirePasswordChanged);

        RequirePasswordToggle = toggle;
        RequirePasswordToggleLabel = toggleLabel;

        PasswordLabel = widgetFactory.CreateTmpText(
            "PasswordLabel",
            parent,
            "PASSWORD:",
            TextAlignmentOptions.TopLeft,
            16f,
            FontStyles.Normal);

        PasswordInput = widgetFactory.CreateInputField(
            "PasswordInput",
            parent,
            string.Empty,
            OnPasswordChanged,
            out Text _,
            out Text _,
            placeholderText: "Room password");
    }

    private void OnRequirePasswordChanged(bool value)
    {
        _options.RequirePasswordForHostedRoom.Value = value;

        if (!value)
        {
            ClearPassword();
        }
    }

    private void OnPasswordChanged(string value)
    {
        _password = value;
    }
}
