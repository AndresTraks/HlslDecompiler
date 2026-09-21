cbuffer ObjectId : register(b0)
{
	int objectIndex;
	uint materialId;
};

int4 main(nointerpolation int2 texcoord : TEXCOORD) : SV_Target
{
	int4 o;

	int r0;
	r0 = texcoord.y << 2;
	o.x = r0.x + objectIndex;
	o.y = -(texcoord.x) + objectIndex;
	o.z = texcoord.y ^ materialId;
	o.w = texcoord.y & texcoord.x;

	return o;
}
