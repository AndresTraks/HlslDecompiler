sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex2D(sampler0, texcoord.xy);
	float4 t1;
	if (texcoord.x > 0) {
		t1 = t0 + tex2D(sampler0, texcoord.zw);
	} else {
		t1 = t0 - tex2D(sampler0, texcoord.wz);
	}
	if (texcoord.y > 0) {
		t0 = tex2D(sampler0, texcoord.yx);
		return t0 * t1;
	} else {
		t0 = tex2D(sampler0, texcoord.xz);
		return t0 + t1;
	}
}
