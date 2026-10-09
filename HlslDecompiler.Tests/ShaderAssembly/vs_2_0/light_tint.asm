vs_2_0
def c10, 0, 1, 0.899999976, 0.800000012
dcl_position v0
dcl_normal v1
dp4 oPos.x, v0, c0
dp4 oPos.y, v0, c1
dp4 oPos.z, v0, c2
dp4 oPos.w, v0, c3
dp3 r0.x, v1.xyz, c4.xyz
dp3 r0.y, v1.xyz, c5.xyz
dp3 r0.z, v1.xyz, c6.xyz
nrm r1.xyz, r0.xyz
dp3 r0.x, r1.xyz, -c8.xyz
max r0.x, r0.x, c10.x
min r0.x, r0.x, c10.y
mul oD0, r0.x, c10.yzwy
dp4 r0.x, v0, c4
dp4 r0.y, v0, c5
dp4 r0.z, v0, c6
add r0.xyz, r0.xyz, -c7.xyz
dp3 r0.x, r0.xyz, r0.xyz
rsq r0.x, r0.x
rcp r0.x, r0.x
add r0.x, -r0.x, c9.y
mul r0.x, r0.x, c9.z
max r0.x, r0.x, c10.x
min oFog, r0.x, c10.y
