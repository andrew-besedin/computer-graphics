using System.Globalization;
using System.IO;
using System.Numerics;

namespace Лаб1WpfApp1
{
    public class Face
    {
        public int[] vIndices;
        public int[]? nIndices;
        public int[]? tIndices;

        public Face(int[] vIndices)
        {
            this.vIndices = vIndices;
        }
    }
    public class Obj
    {
        public List<Vector3> vertices = new();
        public List<Face> faces = new();
        public List<Vector3> normals = new();
        public List<Vector2> uvs = new();
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
                            bool hasTextureIndices = faceParts.Length > 1 && faceParts[1].Length > 0;
                            bool hasNormalIndices = faceParts.Length > 2 && faceParts[2].Length > 0;
                            int[] vertices = new int[(parts.Length - 1)];
                            int[]? textures = hasTextureIndices ? new int[(parts.Length - 1)] : null;
                            int[]? normals = hasNormalIndices ? new int[(parts.Length - 1)] : null;
                            for (int i = 1; i < parts.Length; i++)
                            {
                                faceParts = parts[i].Split('/');
                                vertices[i - 1] = int.Parse(faceParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture) - 1;
                                if (textures != null)
                                {
                                    textures[i - 1] = int.Parse(faceParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture) - 1;
                                }
                                if (normals != null)
                                {
                                    normals[i - 1] = int.Parse(faceParts[2], NumberStyles.Integer, CultureInfo.InvariantCulture) - 1;
                                }
                            }
                            for (int i = 0; i < vertices.Length - 2; i++)
                            {
                                int[] triangleVertices = [vertices[0], vertices[i + 1], vertices[i + 2]];
                                int[]? triangleTextures = hasTextureIndices ? [textures![0], textures[i + 1], textures[i + 2]] : null;
                                int[]? triangleNormals = hasNormalIndices ? [normals![0], normals[i + 1], normals[i + 2]] : null;
                                Face newFace = new(triangleVertices)
                                {
                                    nIndices = triangleNormals,
                                    tIndices = triangleTextures,
                                };
                                obj.faces.Add(newFace);
                            }

                        }
                        break;
                    case "vn":
                    {
                        Vector3 newNormal;
                        newNormal.X = Single.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                        newNormal.Y = Single.Parse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture);
                        newNormal.Z = Single.Parse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture);

                        newNormal = Vector3.Normalize(newNormal);

                        obj.normals.Add(newNormal);
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
                    if (face.nIndices != null)
                    {
                        int n = face.nIndices[i];
                        if (n < 0)
                            n = obj.normals.Count + n + 1;
                        face.nIndices[i] = n;
                    }
                    if (face.tIndices != null)
                    {
                        int t = face.tIndices[i];
                        if (t < 0)
                            t = obj.uvs.Count + t + 1;
                        face.tIndices[i] = t;
                    }
                }
            }
            return obj;
        }
    }
}
