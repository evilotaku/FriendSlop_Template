using UnityEngine;
using Box3D;
using Unity.Scripting.LifecycleManagement;

public partial class Box3DManager : MonoBehaviour
{
    [AutoStaticsCleanup]
    public static World PhysWorld {  get; private set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PhysWorld = World.Create(WorldDef.Default);
       
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
