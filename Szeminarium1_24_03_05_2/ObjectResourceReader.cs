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
            List<float[]> objVertices = new List<float[]>();
            List<int[]> objFaces = new List<int[]>();

            string fullResourceName = "Szeminarium1_24_03_05_2.Resources." + resourceName;
            using (var objStream = typeof(ObjectResourceReader).Assembly.GetManifestResourceStream(fullResourceName))
            using (var objReader = new StreamReader(objStream))
            {
                while (!objReader.EndOfStream)
                {
                    var line = objReader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var lineClassifier = line.Substring(0, line.IndexOf(' '));
                    var lineData = line.Substring(line.IndexOf(" ")).Trim().Split(' ');

                    switch (lineClassifier)
                    {
                        case "v":
                            float[] vertex = new float[3];
                            for (int i = 0;i<vertex.Length; ++i)
                                vertex[i] = float.Parse(lineData[i], CultureInfo.InvariantCulture);
                            objVertices.Add(vertex);
                            break;
                        case "f":
                            int[] face = new int[3];
                            for (int i = 0; i < face.Length; ++i)
                                face[i] = int.Parse(lineData[i], CultureInfo.InvariantCulture);
                            objFaces.Add(face);
                            break;
                        default:
                            throw new Exception("Unhandled obj structure.");
                    }
                }
            }

            List<ObjVertexTransformationData> vertexTransformations = new List<ObjVertexTransformationData>();
            foreach (var objVertex in objVertices)
            {
                vertexTransformations.Add(new ObjVertexTransformationData(
                    new Vector3D<float>(objVertex[0], objVertex[1], objVertex[2]),
                    Vector3D<float>.Zero,
                    0
                    ));
            }

            foreach (var objFace in objFaces)
            {
                var a = vertexTransformations[objFace[0] - 1];
                var b = vertexTransformations[objFace[1] - 1];
                var c = vertexTransformations[objFace[2] - 1];

                var normal = Vector3D.Normalize(Vector3D.Cross(b.Coordinates - a.Coordinates, c.Coordinates - a.Coordinates));

                a.UpdateNormalWithContributionFromAFace(normal);
                b.UpdateNormalWithContributionFromAFace(normal);
                c.UpdateNormalWithContributionFromAFace(normal);
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

                glColors.AddRange([1.0f, 0.0f, 0.0f, 1.0f]);
            }

            List<uint> glIndexArray = new List<uint>();
            foreach (var objFace in objFaces)
            {
                glIndexArray.Add((uint)(objFace[0] - 1));
                glIndexArray.Add((uint)(objFace[1] - 1));
                glIndexArray.Add((uint)(objFace[2] - 1));
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
