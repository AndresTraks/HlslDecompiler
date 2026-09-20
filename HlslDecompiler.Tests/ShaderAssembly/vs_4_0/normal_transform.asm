vs_4_0
dcl_constantbuffer CB0[10], immediateIndexed
dcl_input v0
dcl_input v1.xyz
dcl_input v2.xyz
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_output o2.xyz
dcl_output_siv o3.x, clip_distance
dcl_temps 3
dp4 r0.x, v0, cb0[0]
dp4 r0.y, v0, cb0[1]
dp4 r0.z, v0, cb0[2]
dp4 r0.w, v0, cb0[3]
dp4 o0.x, r0, cb0[4]
dp4 o0.y, r0, cb0[5]
dp4 o0.z, r0, cb0[6]
dp4 o0.w, r0, cb0[7]
dp4 o3.x, r0, cb0[8]
mul r0.xyz, v1.yyy, cb0[1].xyz
mad r0.xyz, cb0[0].xyz, v1.xxx, r0.xyz
mad r0.xyz, cb0[2].xyz, v1.zzz, r0.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
mov o1.xyz, r0.xyz
dp3 r1.x, v2.xyz, cb0[0].xyz
dp3 r1.y, v2.xyz, cb0[1].xyz
dp3 r1.z, v2.xyz, cb0[2].xyz
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
mul r2.xyz, r0.zxy, r1.yzx
mad r2.xyz, r0.yzx, r1.zxy, -r2.xyz
dp3 o2.z, r0.xyz, cb0[9].xyz
dp3 o2.x, r1.xyz, cb0[9].xyz
dp3 o2.y, r2.xyz, cb0[9].xyz
ret
