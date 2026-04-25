using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginUI : MonoBehaviour
{
    [SerializeField] GameObject loginPanel;
    [SerializeField] TMP_InputField nicknameInput;
    [SerializeField] Button joinButton;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] Camera lobbyCamera;

    SessionLauncher sessionLauncher;

    void Awake()
    {
        sessionLauncher = FindFirstObjectByType<SessionLauncher>();
        joinButton.onClick.AddListener(OnJoinClicked);
        joinButton.interactable = false;
        nicknameInput.onValueChanged.AddListener(OnNicknameChanged);
    }

    void OnNicknameChanged(string value)
    {
        joinButton.interactable = !string.IsNullOrWhiteSpace(value);
    }

    async void OnJoinClicked()
    {
        string nickname = nicknameInput.text.Trim();
        if (string.IsNullOrWhiteSpace(nickname))
            return;

        joinButton.interactable = false;
        nicknameInput.interactable = false;
        statusText.text = "Connecting...";

        bool success = await sessionLauncher.StartSession(nickname);

        if (success)
        {
            statusText.text = "Connected!";
            loginPanel.SetActive(false);

            if (lobbyCamera != null)
                lobbyCamera.gameObject.SetActive(false);
        }
        else
        {
            statusText.text = "Connection failed. Try again.";
            joinButton.interactable = true;
            nicknameInput.interactable = true;
        }
    }
}
