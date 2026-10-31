float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = (-texcoord.yx < texcoord.yx) ? 1 : 0;
	return float4(1, t0.y * texcoord.x, t0.y * t0.x * pow(abs(texcoord.y), k.x), 1) + pow(abs(texcoord), k.y) + smoothstep(k.z, k.w, texcoord);
}
