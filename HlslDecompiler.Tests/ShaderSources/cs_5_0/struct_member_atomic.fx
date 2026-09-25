struct CellsElement
{
	uint count;
	uint mask;
};

RWStructuredBuffer<CellsElement> cells : register(u0);

[numthreads(8, 1, 1)]
void main()
{
	InterlockedAdd(cells[0].count, 1);
	InterlockedOr(cells[0].mask, 2);
}
