
using System.Collections.Generic;
using Godot;



namespace PPO.Aero;



public struct Moment(Vector3 force, Vector3 offset)
{
    public Vector3 Force = force;
    public Vector3 Offset = offset;



    public static Moment Zero => new();
    public static Moment FromForce(Vector3 force) => new(force, Vector3.Zero);



    public static Moment Sum(params Moment[] moments)
    {
        Vector3 offset = Vector3.Zero;
        Vector3 force = Vector3.Zero;
        float totalMag = 0.0f;
        for (int i = 0; i < moments.Length; i++)
        {
            float mag = moments[i].Force.Length();
            offset += mag * moments[i].Offset;
            force += moments[i].Force;
            totalMag += mag;
        }
        return totalMag > 0.0f ? new(force, offset / totalMag) : new(force, Vector3.Zero);
    }



    public static (Vector3 CentralForce, Vector3 Torque) SumForceTorque(Moment[] moments)
    {
        Vector3 centralForce = Vector3.Zero;

        Vector3 torque = Vector3.Zero;

        for (int moment = 0; moment < moments.Length; moment++)
        {
            centralForce += moments[moment].Force;

            torque += moments[moment].Offset.Cross(moments[moment].Force);
        }

        return (centralForce, torque);
    }



    public static Moment operator *(Basis basis, Moment moment)
        => new(basis * moment.Force, basis * moment.Offset);



    public static Moment operator *(Transform3D transform, Moment moment)
        => new(transform.Basis * moment.Force, transform * moment.Offset);



    public static Moment operator +(Moment a, Moment b)
    {
        var aMag = a.Force.Length();
        var bMag = b.Force.Length();

        var offset = (a.Offset * aMag + b.Offset * bMag) / (aMag + bMag);
        var force = a.Force + b.Force;

        return new(force, offset);
    }



    public override readonly string ToString()
    {
        return $"Moment(Force: {Force}, Offset: {Offset}";
    }
}