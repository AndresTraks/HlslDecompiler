sampler2D heightMap;
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : POSITION
{
	float4 o;

	float4 r0;
	r0 = float4(1, 1, 0, 0) * i.texcoord.xyxx;
	r0 = tex2Dlod(heightMap, r0);
	r0 = r0.x * float4(0, 1, 0, 0) + i.position;
	o.x = dot(r0, transpose(wvp)[0]);
	o.y = dot(r0, transpose(wvp)[1]);
	o.z = dot(r0, transpose(wvp)[2]);
	o.w = dot(r0, transpose(wvp)[3]);

	return o;
}
