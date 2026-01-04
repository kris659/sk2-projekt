using UnityEngine;

[CreateAssetMenu(fileName ="PlayerTypeData", menuName = "PlayerTypeData")]
public class PlayerTypeData : ScriptableObject
{
    public GameObject VisualPrefab;
    public int StartingHealth;
    public float MovementSpeed;
    public float FireRate;
    public int BulletDamage;
    public float BulletSize;
    public float BulletSpeed;
}
