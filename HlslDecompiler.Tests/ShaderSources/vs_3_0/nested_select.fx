float4 k : register(c4);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.position = mul(i.position, wvp);
	o.color = float4(lerp(k.z, lerp(k.y, k.x, (i.color.y < 0) ? 1 : 0), (i.color.x < 0) ? 1 : 0), clamp(k.y, i.color.y, max(k.x, i.color.x)), lerp(k.x, k.y, i.color.w) * ((i.color.z >= k.z) ? 1 : 0), (k.w - i.color.z) * -(i.color.x - i.color.y));

	return o;
}
