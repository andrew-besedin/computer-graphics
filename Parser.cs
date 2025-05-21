using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Web;
using System.Windows.Media.Media3D;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Лаб1WpfApp1
{
    public class Face
    {
        public int[] vIndices;
        public int[]? nIndices;
        public int[]? tIndices;
        public Material material;

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
        public List<Material> materials = new();
        public Obj() { }
    }

    public struct ImageData
    {
        public Bgra32[] Pixels { get; }
        public int Width { get; }
        public int Height { get; }

        public ImageData(Bgra32[] pixels, int width, int height)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
        }

        public readonly Vector4 SampleNearest(Vector2 uv)
        {
            uv.X = float.Clamp(uv.X, 0, 1);
            uv.Y = float.Clamp(1 - uv.Y, 0, 1);
            int x = (int)MathF.Round((uv.X * (Width - 1)));
            int y = (int)MathF.Round((uv.Y * (Height - 1)));
            return Pixels[y * Width + x].ToScaledVector4();
        }
    }
    public class CubeMappingTextures
    {
        public ImageData PositiveX { get; private set; }
        public ImageData NegativeX { get; private set; }
        public ImageData PositiveY { get; private set; }
        public ImageData NegativeY { get; private set; }
        public ImageData PositiveZ { get; private set; }
        public ImageData NegativeZ { get; private set; }

        public CubeMappingTextures(string filePath)
        {
            using Image<Bgra32> image = Image.Load<Bgra32>(filePath);

            int faceWidth = image.Width / 4;
            int faceHeight = image.Height / 3;

            PositiveX = ExtractFace(image, 2 * faceWidth, 1 * faceHeight, faceWidth, faceHeight);
            NegativeX = ExtractFace(image, 0 * faceWidth, 1 * faceHeight, faceWidth, faceHeight);
            PositiveY = ExtractFace(image, 1 * faceWidth, 0 * faceHeight, faceWidth, faceHeight);
            NegativeY = ExtractFace(image, 1 * faceWidth, 2 * faceHeight, faceWidth, faceHeight);
            PositiveZ = ExtractFace(image, 3 * faceWidth, 1 * faceHeight, faceWidth, faceHeight);
            NegativeZ = ExtractFace(image, 1 * faceWidth, 1 * faceHeight, faceWidth, faceHeight);
        }

        private ImageData ExtractFace(Image<Bgra32> source, int x, int y, int width, int height)
        {
            Bgra32[] pixels = new Bgra32[(width * height)];

            source.Clone(ctx => ctx
                .Crop(new Rectangle(x, y, width, height)))
                .CopyPixelDataTo(pixels);

            return new ImageData(pixels, width, height);
        }

        public Vector4 SampleBackground(Vector3 direction)
        {
            direction = Vector3.Normalize(direction);

            float absX = MathF.Abs(direction.X);
            float absY = MathF.Abs(direction.Y);
            float absZ = MathF.Abs(direction.Z);

            ImageData face;
            Vector2 uv;

            if (absX >= absY && absX >= absZ)
            {
                if (direction.X > 0)
                {
                    face = PositiveX;
                    uv = new Vector2(direction.Z, direction.Y) / absX;
                }
                else
                {
                    face = NegativeX;
                    uv = new Vector2(-direction.Z, direction.Y) / absX;
                }
            }
            else if (absY >= absX && absY >= absZ)
            {
                if (direction.Y > 0)
                {
                    face = PositiveY;
                    uv = new Vector2(direction.X, direction.Z) / absY;
                }
                else
                {
                    face = NegativeY;
                    uv = new Vector2(direction.X, -direction.Z) / absY;
                }
            }
            else
            {
                if (direction.Z > 0)
                {
                    face = PositiveZ;
                    uv = new Vector2(-direction.X, direction.Y) / absZ;
                }
                else
                {
                    face = NegativeZ;
                    uv = new Vector2(direction.X, direction.Y) / absZ;
                }
            }

            uv = (uv + Vector2.One) * 0.5f;

            return face.SampleNearest(uv);
        }
    }
    public class Material
    {
        public string Name { get; set; } = "";
        public float[] Ka { get; set; } = new float[3];
        public float[] Kd { get; set; } = new float[3];
        public float[] Ks { get; set; } = new float[3];
        public float[] Ke { get; set; } = new float[3];
        public float Ns { get; set; }
        public float Ni { get; set; } = 50f;
        public float d { get; set; }
        public int illum { get; set; }

        public ImageData? Map_Ka { get; set; }
        public ImageData? Map_Kd { get; set; }
        public ImageData? Map_Ks { get; set; }
        public ImageData? Map_Ns { get; set; }
        public ImageData? Map_d { get; set; }
        public ImageData? Map_Bump { get; set; }
        public ImageData? Bump { get; set; }
        public ImageData? Disp { get; set; }
        public ImageData? Decal { get; set; }
    }

    public class Parser
    {
        private static float[] ParseVector3(string[] parts)
        {
            var result = new float[3];
            if (parts.Length >= 4)
            {
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out result[0]);
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out result[1]);
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out result[2]);
            }
            return result;
        }

        private static string JoinRest(string[] parts)
        {
            return string.Join(' ', parts, 1, parts.Length - 1);
        }

        private static ImageData? LoadImage(string basePath, string relativePath)
        {
            string fullPath = Path.Combine(basePath, relativePath);
            if (!File.Exists(fullPath))
                return null;

            using Image<Bgra32> image = Image.Load<Bgra32>(fullPath);
            var pixels = new Bgra32[image.Width * image.Height];
            image.CopyPixelDataTo(pixels);
            return new ImageData(pixels, image.Width, image.Height);
        }
        public static Dictionary<string, Material> ParseMtlFile(string filename)
        {
            var materials = new Dictionary<string, Material>();
            Material? currentMaterial = null;
            string basePath = Path.GetDirectoryName(filename) ?? "";

            using FileStream mtlFileStream = new(filename, FileMode.Open);
            using StreamReader mtlSr = new(mtlFileStream);

            while (!mtlSr.EndOfStream)
            {
                string? line = mtlSr.ReadLine();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                switch (parts[0].ToLower())
                {
                    case "newmtl":
                        if (parts.Length > 1)
                        {
                            currentMaterial = new Material { Name = parts[1] };
                            materials[currentMaterial.Name] = currentMaterial;
                        }
                        break;

                    case "ka":
                        currentMaterial!.Ka = ParseVector3(parts);
                        break;

                    case "kd":
                        currentMaterial!.Kd = ParseVector3(parts);
                        break;

                    case "ks":
                        currentMaterial!.Ks = ParseVector3(parts);
                        break;

                    case "ke":
                        currentMaterial!.Ke = ParseVector3(parts);
                        break;

                    case "ns":
                        if (currentMaterial != null && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ns))
                            currentMaterial.Ns = ns;
                        break;

                    case "ni":
                        if (currentMaterial != null && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ni))
                            currentMaterial.Ni = ni;
                        break;

                    case "d":
                        if (currentMaterial != null && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float d))
                            currentMaterial.d = d;
                        break;

                    case "illum":
                        if (currentMaterial != null && int.TryParse(parts[1], out int illum))
                            currentMaterial.illum = illum;
                        break;

                    case "map_ka":
                        if (currentMaterial != null) currentMaterial.Map_Ka = LoadImage(basePath, JoinRest(parts));
                        break;

                    case "map_kd":
                        if (currentMaterial != null) currentMaterial.Map_Kd = LoadImage(basePath, JoinRest(parts));
                        break;

                    case "map_ks":
                        if (currentMaterial != null) currentMaterial.Map_Ks = LoadImage(basePath, JoinRest(parts));
                        break;

                    case "map_ns":
                        if (currentMaterial != null) currentMaterial.Map_Ns = LoadImage(basePath, JoinRest(parts));
                        break;

                    case "map_d":
                        if (currentMaterial != null) currentMaterial.Map_d = LoadImage(basePath, JoinRest(parts));
                        break;

                    case "map_bump":
                        if (currentMaterial != null) currentMaterial.Map_Bump = LoadImage(basePath, JoinRest(parts));
                        break;

                    case "bump":
                        if (currentMaterial != null) currentMaterial.Bump = LoadImage(basePath, JoinRest(parts));
                        break;

                    case "disp":
                        if (currentMaterial != null) currentMaterial.Disp = LoadImage(basePath, JoinRest(parts));
                        break;

                    case "decal":
                        if (currentMaterial != null) currentMaterial.Decal = LoadImage(basePath, JoinRest(parts));
                        break;
                }
            }

            return materials;
        }

        public static Obj ParseObjFile(string filePath)
        {
            Dictionary<string, Material> materialsBuf = new();

            string vertexMaterialNameBuf = null;

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
                    case "mtllib":
                        {
                            var dirPath = Path.GetDirectoryName(filePath);

                            var mtlFileLink = HttpUtility.UrlDecode(parts[1]);
                            var parsedMaterials = ParseMtlFile(Path.Combine(dirPath, mtlFileLink));

                            parsedMaterials.ToList().ForEach(material => materialsBuf[material.Key] = material.Value);
                        }
                        break;
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
                            if (vertexMaterialNameBuf == null)
                            {
                                throw new ArgumentNullException("Face declaration reached before usemtl declaration.");
                            }
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

                            Face newFace = new(vertices)
                            {
                                nIndices = normals,
                                tIndices = textures,
                                material = materialsBuf[vertexMaterialNameBuf]
                            };
                            obj.faces.Add(newFace);


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
                    case "vt":
                    {
                        Vector2 newTextureCoord;
                        newTextureCoord.X = Single.Parse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture);
                        newTextureCoord.Y = Single.Parse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture);
                        obj.uvs.Add(newTextureCoord);
                    }
                    break;
                    case "usemtl":
                    {
                        vertexMaterialNameBuf = parts[1];
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
