float a;
float b;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return texcoord.y < b && a < texcoord.x ? texcoord : -texcoord;
}
