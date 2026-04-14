using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.Hexa.ImGui;
using Silk.NET.Windowing;
using System.Numerics;

namespace WoWRenderLib
{
    public class SilkImGuiBackend : IImGuiBackend
    {
        private ImGuiController controller;
        private IWindow window;
        private IInputContext input;
        private GL gl;

        public SilkImGuiBackend(GL gl, IWindow window, IInputContext input)
        {
            this.gl = gl;
            this.window = window;
            this.input = input;
        }

        public void Initialize()
        {
            controller = new ImGuiController(
                gl,
                window,
                input,
                null,
                () => ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable
            );

            ImGui.GetStyle().WindowRounding = 5.0f;
            ImGui.GetStyle().WindowPadding = new Vector2(0.0f, 0.0f);
            ImGui.GetStyle().FrameRounding = 12.0f;
            ImGuizmo.SetImGuiContext(controller.Context);

        }

        public void Update(float deltaTime)
        {
            controller.Update(deltaTime);
        }

        public void Render()
        {
            controller.Render();
        }

        public void Dispose()
        {
            controller.Dispose();
        }
    }
}
