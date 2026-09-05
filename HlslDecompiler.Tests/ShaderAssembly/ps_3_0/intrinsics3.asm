ps_3_0
dcl_normal v0.xyz
dcl_texcoord v1.xyz
mov r0.xyz, v0.xyz
mul r1.xyz, r0.zxy, v1.yzx
mad r0.xyz, r0.yzx, v1.zxy, -r1.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul oC0.xyz, r0.www, r0.xyz
mov r0.xyz, c0.xyz
add r1.xyz, r0.xyz, -c1.xyz
dp3 r0.z, r1.xyz, r1.xyz
rsq r0.z, r0.z
rcp r0.z, r0.z
dp2add oC0.w, r0.xy, c1.xy, r0.zz
