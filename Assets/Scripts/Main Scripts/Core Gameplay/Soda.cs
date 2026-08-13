using UnityEngine;

public class Soda : MonoBehaviour
{
    public enum SodaColor
    {
        Red,
        Blue,
        Orange,

        // Renamed from Yellow. The gameplay material for this slot (Mat003 on
        // Bottle.prefab) has always been pink - RGB(1.00, 0.38, 0.66) - but the
        // name, the Orders icon and the effect tint were never updated, so the
        // same drink appeared pink on the board and yellow in the UI.
        // The position in the enum is unchanged, so every scene, level asset and
        // saved order that stores this value as an integer keeps working.
        Pink,

        Green,
        Purple
    }

    public Material[] sodaMaterials;  // Array to hold the materials for each soda color
    [SerializeField]private Renderer rend;
    public SodaColor sodaColor;  // Variable to store the soda's color

    private void Start()
    {
        //rend = GetComponentInChildren<Renderer>();
    }

    public void SetColor(SodaColor color)
    {
        // Store the chosen color in the sodaColor variable
        sodaColor = color;

        // Set the material based on the selected color
        switch (color)
        {
            case SodaColor.Red:
                rend.material = sodaMaterials[0];
                break;
            case SodaColor.Blue:
                rend.material = sodaMaterials[1]; 
                break;
            case SodaColor.Orange:
                rend.material = sodaMaterials[2];
                break;
            case SodaColor.Pink:
                rend.material = sodaMaterials[3];
                break;
            case SodaColor.Green:
                rend.material = sodaMaterials[4];
                break;
            case SodaColor.Purple:
                rend.material = sodaMaterials[5];
                break;

        }
    }
    public void SetColor1(SodaColor color)
    {
        sodaColor = color;

        // Check if we have the correct number of materials
        if (sodaMaterials.Length > (int)color)
        {
            // Assign the corresponding material to the soda's renderer
            rend.material = sodaMaterials[(int)color];
        }
        else
        {
            Debug.LogWarning("Not enough materials assigned in sodaMaterials array for all soda colors.");
        }
    }
}
