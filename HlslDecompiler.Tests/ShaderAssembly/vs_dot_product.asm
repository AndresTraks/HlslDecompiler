vs_3_0
def c2, 3, 4, 0, 0
dcl_position v0
dcl_texcoord v1
dcl_position o0
dp4 o0.x, c1, v0
dp3 o0.y, c0.xyz, v1.xyz
mov o0.zw, c2.xy
