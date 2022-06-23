vs_3_0
def c0, 1, 0, 0, 0
dcl_position v0
dcl_position o0
dp3 r0.x, v0.xyz, v0.xyz
rsq r0.x, r0.x
mul o0.xyz, r0.xxx, v0.yxz
mov o0.w, c0.x
