using UnityEngine;
using Unity.Pipeline.Commands;

public static class CreateCubeCommands
{
    [CliCommand("create_cube", "Create a Cube GameObject at the specified position")]
    public static string CreateCube([CliArg("position")] string position, string name = "Cube")
    {
        var go = new GameObject(name);

        // Parse position
        float.TryParse(position.Split(',')[0], out float x);
        float.TryParse(position.Split(',')[1].Split(':')[1], out float y);
        float.TryParse(position.Split()[3].Split(':')[1], out float z);

        go.transform.position = new Vector3(x, y, z);

        return $"Created {go.name} at position ({x}, {y}, {z})";
    }
}
