
using System;
using System.Text;
using Godot;



namespace PPO.Ui;



public partial class Hud : Control
{
    [Export]
    private Label _inputLabel;

    [Export]
    private Label _outputLabel;

    [Export]
    private Label _rewardLabel;

    [Export]
    private Label _statsLabel;

    public int HitCount;

    public string[] InputNames;
    public Func<float[]> InputData;

    public string[] OutputNames;
    public Func<float[]> OutputData;



    public override void _Process(double delta)
    {
        if (InputNames is not null && InputData is not null)
        {
            _inputLabel.Text = BuildString(InputNames, InputData());
        }

        if (OutputNames is not null && OutputData is not null)
        {
            _outputLabel.Text = BuildString(OutputNames, OutputData());
        }

        _rewardLabel.Text = $"Hits: {HitCount}";
    }



    private static string BuildString(string[] names, float[] data)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < Math.Min(names.Length, data.Length); i++)
        {
            sb.AppendLine($"{names[i]}: {data[i],10:00.0000}");
        }

        return sb.ToString();
    }
}
