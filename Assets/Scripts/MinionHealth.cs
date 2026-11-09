using UnityEngine;
using TMPro;

public class MinionHealth : MonoBehaviour
{
    private MinionStats stats;
    public int currentHP;

    [Header("UI Referanse")]
    public TextMeshProUGUI hpText; 

    void Awake()
    {
        InitializeStats();
    }

    void Start()
    {
        InitializeStats();
        if (stats != null)
        {
            currentHP = stats.maxHP;
        }
        UpdateHPUI();
    }

    void OnValidate()
    {
        InitializeStats();
        if (stats != null)
        {
            if (currentHP <= 0 || currentHP > stats.maxHP)
            {
                currentHP = stats.maxHP;
            }
        }
        UpdateHPUI();
    }

    // Hjelpemetode for å hente stats og forhindre null-referanser
    void InitializeStats()
    {
        if (stats == null)
        {
            stats = GetComponent<MinionStats>();
        }
    }

    public void TakeDamage(int amount)
    {
        InitializeStats();
        
        int maxHp = stats != null ? stats.maxHP : 10; // Standardfallback hvis stats mangler
        
        currentHP -= amount;
        if (currentHP < 0) currentHP = 0;
        
        Debug.Log("Tok skade! Ny HP er: " + currentHP);
        
        UpdateHPUI();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void UpdateHPUI()
    {
        InitializeStats();

        if (hpText != null && stats != null)
        {
            hpText.text = currentHP + "/" + stats.maxHP + " HP";
            Debug.Log("Oppdaterer UI-tekst til: " + hpText.text);
        }
        else if (hpText != null && stats == null)
        {
            // Viser HP midlertidig selv om MinionStats skulle mangle, for å unngå krasj
            hpText.text = currentHP + "/? HP";
        }
        else
        {
            if (hpText == null)
                Debug.LogWarning("ADVARSEL: hpText mangler referanse i Inspector på " + gameObject.name);
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }
}