sampler2D s;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex2D(s, texcoord.xy / texcoord.w);
	return tex2Dlod(s, texcoord.xyyw) + tex2Dbias(s, texcoord.xyyw) + t0;
}
