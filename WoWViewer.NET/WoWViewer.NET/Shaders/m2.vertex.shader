#version 330

uniform mat4 projection_matrix;
uniform mat4 view_matrix;
uniform mat4 model_matrix;

in vec3 position;
in vec2 texCoord;

out vec2 TexCoord;

void main()
{
	gl_Position = projection_matrix * view_matrix * model_matrix * vec4(position, 1);
	TexCoord = texCoord;
}
