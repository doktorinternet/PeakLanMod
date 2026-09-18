using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakLanMod.Lan.UI;

// Owns the host-side password field widget and state. Password protection is
// inferred from whether the field has a value, with no separate enable toggle.
// Kept as its own collaborator instead of more inline code in LanOverlayController,
// per docs/research/password-protected-rooms-plan.md. Widget construction still
// borrows LanOverlayController's factory methods until the broader extraction in
// docs/research/lan-overlay-ui-refactor-plan.md lands.
internal sealed class HostPasswordFieldController
{
    private string _password = string.Empty;

    internal TMP_Text? PasswordLabel { get; private set; }
    internal InputField? PasswordInput { get; private set; }

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
        if (PasswordLabel != null)
        {
            return;
        }

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
            placeholderText: "Room password (optional)");
    }

    private void OnPasswordChanged(string value)
    {
        _password = value;
    }
}
