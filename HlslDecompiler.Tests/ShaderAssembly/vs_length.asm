vs_3_0
def c0, 2, -5, 0, 0
dcl_position v0
dcl_position1 v1
dcl_position2 v2
dcl_position o0
dp4 r0.x, v0, v0
rsq r0.x, r0.x
rcp o0.x, r0.x
dp3 r0.x, v1.xyz, v1.xyz
rsq r0.x, r0.x
rcp o0.y, r0.x
mul r0.xy, v2.xy, v2.xy
add r0.x, r0.y, r0.x
rsq r0.x, r0.x
rcp r0.x, r0.x
mov o0.z, -r0.x
add r0, c0.x, v2
dp4 r0.x, r0, r0
rsq r0.x, r0.x
rcp r0.x, r0.x
mul o0.w, r0.x, c0.y
