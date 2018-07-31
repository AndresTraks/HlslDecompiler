vs_3_0
def c3, 4, 0, 0, 0
dcl_position v0
dcl_texcoord v1
dcl_texcoord2 v2
dcl_position o0
dp4 o0.x, c2, v0
dp3 o0.y, c1.xyz, v1.xyz
mul r0.xy, c0.xy, v2.xy
add o0.z, r0.y, r0.x
mov o0.w, c3.x
