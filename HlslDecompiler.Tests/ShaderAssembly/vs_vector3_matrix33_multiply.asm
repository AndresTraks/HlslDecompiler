vs_3_0
def c3, 4, 0, 0, 0
dcl_position v0
dcl_position o0
dp3 o0.x, v0.xyz, c0.xyz
dp3 o0.y, v0.xyz, c1.xyz
dp3 o0.z, v0.xyz, c2.xyz
mov o0.w, c3.x
