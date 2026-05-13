using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ButtonsManager : MonoBehaviour
{
    [SerializeField]TriggerButton[] triggerButtons;
    public GameObject floor;
    public static ButtonsManager instance{
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ButtonsManager>(FindObjectsInactive.Include);
            }
            return _instance;
        }
    }
    static ButtonsManager _instance;

      private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this) Destroy(this);
    }
    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void checkButton()
    {
        if(triggerButtons.Any(b=> b.pressed)) floor.SetActive(false);
    }
}
