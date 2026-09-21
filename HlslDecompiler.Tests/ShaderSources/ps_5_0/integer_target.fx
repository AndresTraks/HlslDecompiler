cbuffer ObjectId : register(b0)
{
	int objectIndex;
	uint materialId;
};

int4 main(nointerpolation int2 texcoord : TEXCOORD) : SV_Target
{
	return int4((texcoord.y * 4) + objectIndex, objectIndex - texcoord.x, texcoord.y ^ materialId, texcoord.y & texcoord.x);
}
