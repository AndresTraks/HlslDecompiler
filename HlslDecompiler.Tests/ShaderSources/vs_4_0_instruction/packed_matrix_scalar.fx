int3x3 m;
bool flip;

float4 main(int3 p : P) : SV_Position
{
	float4 o;

	int4 r0;
	int3 r1;
	r0.xy = p.xy * transpose(m)[0].xy;
	r0.x = r0.y + r0.x;
	r0.x = p.z * transpose(m)[0].z + r0.x;
	r1.xy = p.xy * transpose(m)[1].xy;
	r0.w = r1.y + r1.x;
	r0.y = p.z * transpose(m)[1].z + r0.w;
	r1.xy = p.xy * transpose(m)[2].xy;
	r0.w = r1.y + r1.x;
	r0.z = p.z * transpose(m)[2].z + r0.w;
	r1 = -r0.xyz;
	r0.xyz = (flip != 0) ? r1.xyz : r0.xyz;
	o.xyz = (float3)r0.xyz;
	o.w = 1;

	return o;
}
