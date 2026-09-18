int4 bounds;
uint scale;

struct PS_IN
{
	nointerpolation int4 texcoord : TEXCOORD;
	nointerpolation uint2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int3 r0;
	r0.x = i.texcoord.z + -(bounds.z);
	r0.x = max(-(r0.x), r0.x);
	r0.y = ~i.texcoord.w;
	r0.y = r0.y & bounds.w;
	r0.x = -(r0.y) + r0.x;
	o.y = (float)r0.x;
	r0.x = min(i.texcoord1.y, scale);
	r0.x = i.texcoord1.x * scale + r0.x;
	o.z = (float)(uint)r0.x;
	r0.x = (i.texcoord.x < i.texcoord.y) ? -1 : 0;
	r0.y = min(i.texcoord.x, bounds.x);
	r0.z = max(i.texcoord.y, bounds.y);
	r0.x = (r0.x != 0) ? r0.y : r0.z;
	r0.y = r0.z + r0.y;
	o.xw = (float2)r0.yx;

	return o;
}
