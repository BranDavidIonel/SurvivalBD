using UnityEngine;

// On every rabbit placed in the scene (Enviroment/Animals/Rabbits): male or female. In the game the rabbits get a
// label above them ("Male" blue / "Female" pink), and a male and a female that meet make babies (RabbitPopulation).
[DisallowMultipleComponent]
public class RabbitInfo : MonoBehaviour
{
    public enum Sex { Male, Female }
    public Sex sex = Sex.Male;
}
