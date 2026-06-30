using System.Collections;
using UnityEngine;
using TMPro;

public class BoardGameSequence : MonoBehaviour
{
    [Header("Sequence Timing")]
    [SerializeField] private float initialDelay = 1.0f;
    [SerializeField] private float diceJumpDuration = 1.0f;
    [SerializeField] private float textDisplayDuration = 3.0f;
    [SerializeField] private float finalDelayAfterCameraReturns = 2.0f;
    [SerializeField] private float typewriterSpeed = 0.05f; // Tiempo entre letras del mensaje final
    [SerializeField] private float fastTypewriterSpeed = 0.02f; // Tiempo entre letras del mensaje de espera
    
    [Header("Dice Physics")]
    public float diceJumpForce = 3.0f;
    public float diceTorqueForce = 2.0f;
    
    [Header("Scene References")]
    [SerializeField] private Transform[] dices;
    [SerializeField] private GameObject canvasNarrator;
    [SerializeField] private TextMeshProUGUI uiText;
    [SerializeField] private LeanTweenDoor door;
    [SerializeField] private BoardCameraOverride cameraOverride;
    
    [Header("Text Message & Sound")]
    [TextArea] [SerializeField] private string textMessage;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip textAppearSound;

    public void StartSequence()
    {
        StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        // 1. Wait for 1 second after camera reached
        yield return new WaitForSeconds(initialDelay);

        // --- NEW MULTIPLAYER SYNC LOGIC ---
        // Turn on Canvas to show waiting message
        if (canvasNarrator != null)
        {
            canvasNarrator.SetActive(true);
        }

        if (uiText != null)
        {
            yield return StartCoroutine(TypeTextRoutine("Esperando a que todos los integrantes esten en la mesa...", fastTypewriterSpeed));
        }

        // Inform network that this local player is ready
        PlayerMovement localPlayer = null;
        foreach (var p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (p.HasInputAuthority)
            {
                localPlayer = p;
                localPlayer.Rpc_SetReadyAtBoard(true);
                break;
            }
        }

        // Wait until ALL players are ready
        while (true)
        {
            PlayerMovement[] allPlayers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
            bool everyoneReady = true;
            foreach (var p in allPlayers)
            {
                if (!p.IsReadyAtBoard)
                {
                    everyoneReady = false;
                    break;
                }
            }

            // Only break if there is at least 1 player and everyone is ready
            if (everyoneReady && allPlayers.Length > 0)
                break;

            yield return new WaitForSeconds(0.5f); // Check twice per second
        }

        // Un-set ready so it can be re-used later if needed
        if (localPlayer != null)
        {
            localPlayer.Rpc_SetReadyAtBoard(false);
        }

        // Clear text before the sequence begins
        if (uiText != null)
        {
            uiText.text = "";
        }
        
        // Brief pause after everyone is ready
        yield return new WaitForSeconds(1.0f);
        // -----------------------------------

        // 2. Make the dice jump using Physics (Rigidbody)
        if (dices != null)
        {
            foreach (Transform dice in dices)
            {
                if (dice != null)
                {
                    Rigidbody rb = dice.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;

                        // Jump up
                        rb.AddForce(Vector3.up * diceJumpForce, ForceMode.Impulse);
                        
                        // Spin randomly
                        Vector3 randomTorque = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
                        rb.AddTorque(randomTorque * diceTorqueForce, ForceMode.Impulse);
                    }
                }
            }
        }

        // Wait for the dice to land
        yield return new WaitForSeconds(diceJumpDuration);
        
        // Wait 1 extra second before leaving the board
        yield return new WaitForSeconds(1.0f);

        // 3. Return camera to the player automatically
        if (cameraOverride != null)
        {
            cameraOverride.DeactivateView();
        }

        // Wait for camera to return (approx 1 second transition time)
        yield return new WaitForSeconds(1.0f);

        // 4. Turn on Canvas and start Typewriter effect
        if (canvasNarrator != null)
        {
            canvasNarrator.SetActive(true);
        }

        if (uiText != null)
        {
            yield return StartCoroutine(TypeTextRoutine(textMessage, typewriterSpeed));
        }

        // 5. Wait while the player reads the full text
        yield return new WaitForSeconds(textDisplayDuration);

        // 6. Open the door once interaction is completed
        if (door != null)
        {
            door.OpenDoor();
        }

        // 7. Canvas stays a little bit longer, then turns off
        yield return new WaitForSeconds(finalDelayAfterCameraReturns);
        if (canvasNarrator != null)
        {
            canvasNarrator.SetActive(false);
        }
    }

    private IEnumerator TypeTextRoutine(string text, float speed)
    {
        uiText.text = "";

        if (audioSource != null && textAppearSound != null)
        {
            audioSource.clip = textAppearSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        foreach (char letter in text.ToCharArray())
        {
            uiText.text += letter;
            yield return new WaitForSeconds(speed);
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
}
