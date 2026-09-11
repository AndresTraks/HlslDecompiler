sampler2D height;
float morph : register(c4);
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
	float4 r1;
	r0.zw = 0;
	r1 = i.position;
	r1 = -r1 + i.texcoord;
	r1 = morph.x * r1 + i.position;
	r0.xy = r1.xz * 0.01;
	r0 = tex2Dlod(height, r0);
	r1.y = r0.x * 10 + r1.y;
	o.x = dot(r1, transpose(wvp)[0]);
	o.y = dot(r1, transpose(wvp)[1]);
	o.z = dot(r1, transpose(wvp)[2]);
	o.w = dot(r1, transpose(wvp)[3]);

	return o;
}
