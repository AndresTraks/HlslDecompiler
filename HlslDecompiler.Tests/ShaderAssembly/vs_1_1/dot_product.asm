vs_1_1
def c3, 4, 0, 0, 0
dcl_position v0
dcl_texcoord v1
dcl_texcoord1 v2
dp4 oPos.x, c2, v0
dp3 oPos.y, c1.xyz, v1.xyz
mul r0.xy, v2.xy, c0.xy
add oPos.z, r0.y, r0.x
mov oPos.w, c3.x
