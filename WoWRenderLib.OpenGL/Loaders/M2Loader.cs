using Silk.NET.OpenGL;
using WoWRenderLib.OpenGL.Cache;
using WoWRenderLib.OpenGL.Structs;
using WoWRenderLib.Structs;

namespace WoWRenderLib.OpenGL.Loaders
{
    class M2Loader
    {
        private static uint DEFAULT_TEXTURE_ID = 186184; // dungeons/textures/testing/color_01.blp

        public static unsafe ParsedDoodadBatch LoadM2(GL gl, ParsedM2 parsedM2, uint shaderProgram)
        {
            var doodadBatch = new ParsedDoodadBatch()
            {
                boundingBox = parsedM2.boundingBox,
                boundingRadius = parsedM2.boundingRadius,
                fileDataID = parsedM2.fileDataID,
                mats = parsedM2.mats,
                submeshes = parsedM2.submeshes
            };

            foreach (var mat in doodadBatch.mats)
            {
                BLPCache.GetOrLoad(gl, mat.fileDataID, parsedM2.fileDataID);
            }

            doodadBatch.vao = gl.GenVertexArray();
            gl.BindVertexArray(doodadBatch.vao);

            doodadBatch.vertexBuffer = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, doodadBatch.vertexBuffer);
            fixed (byte* buf = parsedM2.vertexBytes)
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(parsedM2.vertexBytes.Length), buf, GLEnum.StaticDraw);

            doodadBatch.indiceBuffer = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, doodadBatch.indiceBuffer);
            fixed (byte* buf = parsedM2.indiceBytes)
                gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(parsedM2.indiceBytes.Length), buf, GLEnum.StaticDraw);

            var posAttrib = gl.GetAttribLocation(shaderProgram, "position");
            gl.EnableVertexAttribArray((uint)posAttrib);
            gl.VertexAttribPointer((uint)posAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 10, (void*)(sizeof(float) * 0));

            var normalAttrib = gl.GetAttribLocation(shaderProgram, "normal");
            gl.EnableVertexAttribArray((uint)normalAttrib);
            gl.VertexAttribPointer((uint)normalAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 10, (void*)(sizeof(float) * 3));

            var texCoord1Attrib = gl.GetAttribLocation(shaderProgram, "texCoord1");
            gl.EnableVertexAttribArray((uint)texCoord1Attrib);
            gl.VertexAttribPointer((uint)texCoord1Attrib, 2, VertexAttribPointerType.Float, false, sizeof(float) * 10, (void*)(sizeof(float) * 6));

            var texCoord2Attrib = gl.GetAttribLocation(shaderProgram, "texCoord2");
            gl.EnableVertexAttribArray((uint)texCoord2Attrib);
            gl.VertexAttribPointer((uint)texCoord2Attrib, 2, VertexAttribPointerType.Float, false, sizeof(float) * 10, (void*)(sizeof(float) * 8));

            return doodadBatch;
        }
    }
}
