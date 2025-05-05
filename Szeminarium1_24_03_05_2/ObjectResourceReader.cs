using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Szeminarium1_24_03_05_2
{
    internal class ObjectResourceReader
    {
        public static unsafe GlObject CreateObjectFromResource(GL Gl, string resourceName)
        {
            List<float[]> objVertices = new List<float[]>(); // v
            List<float[]> objNormals = new List<float[]>(); // vn
            List<(int v, int vn)[]> objFaces = new List<(int, int)[]>(); // f (pl. 1//1 2//2 3//3)

            string fullResourceName = "Szeminarium1_24_03_05_2.Resources." + resourceName;
            using (var objStream = typeof(ObjectResourceReader).Assembly.GetManifestResourceStream(fullResourceName))
            using (var objReader = new StreamReader(objStream))
            {
                while (!objReader.EndOfStream)
                {
                    var line = objReader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line) || !line.Contains(' ')) // skipping empty lines
                        continue;

                    var lineClassifier = line.Substring(0, line.IndexOf(' ')); // v, vn or f type
                    var lineData = line.Substring(line.IndexOf(" ")).Trim().Split(' '); // put into an array

                    switch (lineClassifier)
                    {
                        case "v":
                            float[] vertex = new float[3];
                            for (int i = 0; i < 3; i++)
                                vertex[i] = float.Parse(lineData[i], CultureInfo.InvariantCulture);
                            objVertices.Add(vertex);
                            break;

                        case "vn":
                            float[] normal = new float[3];
                            for (int i = 0; i < 3; i++)
                                normal[i] = float.Parse(lineData[i], CultureInfo.InvariantCulture);
                            objNormals.Add(normal);
                            break;

                        case "f": // a haromszog 3 csucsa
                            var face = new (int, int)[3];
                            for (int i = 0; i < 3; i++)
                            {
                                var parts = lineData[i].Split('/');
                                int vertexIndex = int.Parse(parts[0]) - 1; // csucs index
                                int normalIndex = parts.Length > 1 ? int.Parse(parts[2]) - 1 : -1; // normal index
                                face[i] = (vertexIndex, normalIndex);
                            }
                            objFaces.Add(face);
                            break;

                        default:
                            break;
                    }

                }
            }

            List<ObjVertexTransformationData> vertexTransformations = new List<ObjVertexTransformationData>();
            for (int i = 0; i < objVertices.Count; i++)
            {
                var coords = objVertices[i];
                Vector3D<float> normal = Vector3D<float>.Zero; // zero, mert lehet, hogy nincsenek normalvektorok

                if (i < objNormals.Count)
                {
                    var norm = objNormals[i];
                    normal = new Vector3D<float>(norm[0], norm[1], norm[2]);
                }

                vertexTransformations.Add(new ObjVertexTransformationData(
                    new Vector3D<float>(coords[0], coords[1], coords[2]), // pozicio
                    normal, // normalvektor
                    0 // egyeb parameter
                ));
            }


            if (objNormals.Count == 0) // ha nincs normalvektor, akkor kiszamoljuk a haromszogekbol
            {
                Console.WriteLine("No normals found. Computing them from face geometry...");
                foreach (var face in objFaces)
                {
                    var a = vertexTransformations[face[0].v];
                    var b = vertexTransformations[face[1].v];
                    var c = vertexTransformations[face[2].v];

                    var normal = Vector3D.Normalize(Vector3D.Cross(b.Coordinates - a.Coordinates, c.Coordinates - a.Coordinates)); // keresztszorzat

                    a.UpdateNormalWithContributionFromAFace(normal);
                    b.UpdateNormalWithContributionFromAFace(normal);
                    c.UpdateNormalWithContributionFromAFace(normal);
                }
            }


            List<float> glVertices = new List<float>();
            List<float> glColors = new List<float>();
            foreach (var vertexTransformation in vertexTransformations)
            {
                glVertices.Add(vertexTransformation.Coordinates.X);
                glVertices.Add(vertexTransformation.Coordinates.Y);
                glVertices.Add(vertexTransformation.Coordinates.Z);

                glVertices.Add(vertexTransformation.Normal.X);
                glVertices.Add(vertexTransformation.Normal.Y);
                glVertices.Add(vertexTransformation.Normal.Z);

                glColors.AddRange([1.0f, 0.0f, 1.0f, 1.0f]);
            }

            List<uint> glIndexArray = new List<uint>();
            foreach (var face in objFaces)
            {
                glIndexArray.Add((uint)face[0].v);
                glIndexArray.Add((uint)face[1].v);
                glIndexArray.Add((uint)face[2].v);
            }


            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            uint offsetPos = 0;
            uint offsetNormals = offsetPos + 3 * sizeof(float);
            uint vertexSize = offsetNormals + 3 * sizeof(float);

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertices);
            Gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)glVertices.ToArray().AsSpan(), BufferUsageARB.StaticDraw);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);
            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, true, vertexSize, (void*)offsetNormals);
            Gl.EnableVertexAttribArray(2);


            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, colors);
            Gl.BufferData(BufferTargetARB.ArrayBuffer, (ReadOnlySpan<float>)glColors.ToArray().AsSpan(), BufferUsageARB.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);


            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indices);
            Gl.BufferData(BufferTargetARB.ElementArrayBuffer, (ReadOnlySpan<uint>)glIndexArray.ToArray().AsSpan(), BufferUsageARB.StaticDraw);

            // make sure to unbind array buffer
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            uint indexArrayLength = (uint)glIndexArray.Count;

            Gl.BindVertexArray(0);

            return new GlObject(vao, vertices, colors, indices, indexArrayLength, Gl);
        }
    }
}
