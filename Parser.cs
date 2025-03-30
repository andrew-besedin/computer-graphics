using System.Globalization;
using System.IO;
using System.Numerics;

namespace Лаб1WpfApp1
{
    public class Face
    {
        public int[] vIndices;

        public Face(int[] vIndices)
        {
            this.vIndices = vIndices;
        }
    }
    public class Obj
    {
        public List<Vector3> vertices = new();
        public List<Face> faces = new();
        public Obj() { }
    }

    public class Parser
    {
        public static Obj ParseObjFile(string filePath)
        {
            Obj obj = new();
            using FileStream fileStream = new(filePath, FileMode.Open);
            using StreamReader sr = new(fileStream);
            while (!sr.EndOfStream)
            {
                string? line = sr.ReadLine();
                if (line == null || line.Length == 0)
                {
                    continue;
                }
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "v":
                        {
                            Vector3 newVertex;
                            newVertex.X = Single.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                            newVertex.Y = Single.Parse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture);
                            newVertex.Z = Single.Parse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture);
                            if (parts.Length == 5)
                            {
                                Single w = Single.Parse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture);
                                newVertex /= w;
                            }
                            obj.vertices.Add(newVertex);
                        }
                        break;
                    case "f":
                        {
                            int[,] indices = new int[parts.Length - 1, 3];
                            string[] faceParts = parts[1].Split('/');
                            int[] vertices = new int[(parts.Length - 1)];
                            for (int i = 1; i < parts.Length; i++)
                            {
                                faceParts = parts[i].Split('/');
                                vertices[i - 1] = int.Parse(faceParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture) - 1;
                            }

                            Face newFace = new(vertices);
                            obj.faces.Add(newFace);
                        }
                        break;
                    case "#":
                    default:
                        break;
                }
            }

            foreach (var face in obj.faces)
            {
                for (int i = 0; i < face.vIndices.Length; i++)
                {
                    int p = face.vIndices[i];
                    if (p < 0)
                        p = obj.vertices.Count + p + 1;
                    face.vIndices[i] = p;
                }
            }
            return obj;
        }
    }
}
