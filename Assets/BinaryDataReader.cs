using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static NBodySimulation;

namespace Assets
{
  public class BinaryDataReader
  {

    public static Body[] LoadData(string filePath)
    {
      List<Body> bodies = new List<Body>();

      using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
      {
        int count = reader.ReadInt32(); // Number of planets

        for (int i = 0; i < count; i++)
        {
          var body = new Body
          {
            position = new Vector3
            {
              x = BitConverter.ToSingle(reader.ReadBytes(4), 0),
              y = BitConverter.ToSingle(reader.ReadBytes(4), 0),
              z = BitConverter.ToSingle(reader.ReadBytes(4), 0)
            },
            velocity = new Vector3
            {
              x = BitConverter.ToSingle(reader.ReadBytes(4), 0),
              y = BitConverter.ToSingle(reader.ReadBytes(4), 0),
              z = BitConverter.ToSingle(reader.ReadBytes(4), 0)
            },
            mass = BitConverter.ToSingle(reader.ReadBytes(4), 0)
          };
          if (IsValidVector(body.position) && IsValidVector(body.velocity))
          {
            bodies.Add(body);
          }
        }
      }
      return bodies.ToArray();
    }

    /// <summary>
    /// Returns true if none of the vector's components are NaN or Infinity.
    /// </summary>
    static bool IsValidVector(Vector3 v)
    {
      return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
               float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
  }
}
