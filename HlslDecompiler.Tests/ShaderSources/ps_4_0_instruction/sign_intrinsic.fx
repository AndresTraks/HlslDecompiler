float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	int4 r2;
	r0.x = -(k.x) + k.y;
	r0.x = float1(1) / r0.x;
	r1 = texcoord + -(k.x);
	r0 = saturate(r0.x * r1);
	r1 = r0 * float4(-2, -2, -2, -2) + float4(3, 3, 3, 3);
	r0 = r0 * r0;
	r2 = max(texcoord, -(k.z));
	r2 = min(r2, k.z);
	r0 = r1 * r0 + r2;
	r1 = texcoord / k.w;
	r2 = (r1 >= -(r1)) ? -1 : 0;
	r1 = frac(abs(r1));
	r1 = (r2 != 0) ? r1 : -(r1);
	r0 = r1 * k.w + r0;
	r1 = (float4(0, 0, 0, 0) < texcoord) ? -1 : 0;
	r2 = (texcoord < float4(0, 0, 0, 0)) ? -1 : 0;
	r1 = -(r1) + r2;
	r1 = (float4)(int4)r1;
	r2.xy = texcoord.xy + -(k.xy);
	r2.x = dot(r2.xy, r2.xy);
	r2.x = sqrt(r2.x);
	o = r1 * r2.x + r0;

	return o;
}
