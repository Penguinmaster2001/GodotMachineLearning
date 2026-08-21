
using Godot;



namespace PPO.Envs.Chase;



public partial class TargetNode : Node3D
{
	[Export]
	public MeshInstance3D Mesh;


	
	public void SetColor(Color color)
	{
		Mesh.MaterialOverride = new StandardMaterial3D()
		{
			AlbedoColor = color,
		};
	}
}
