vs_1_1
def c0, 1, 0, 0, 0
dcl_position v0
dp3 r0.x, v0.xyz, v0.xyz
rsq r0.x, r0.x
mul oPos.xyz, r0.xxx, v0.yxz
mov oPos.w, c0.x
