vs_4_0
dcl_constantbuffer cb0[3], immediateIndexed
dcl_input v0.xyz
dcl_output o0
dcl_output o1.xyz
dcl_output o2.xyz
dcl_output o3.xyz
dcl_temps 2
mul r0.xyz, v0.yyy, cb0[1].xyz
mad r1.xyz, cb0[0].xyz, v0.xxx, r0.xyz
mad o0.xyz, cb0[2].xyz, v0.zzz, r1.xyz
mov o0.w, l(1)
mul r1.xyz, v0.xxx, cb0[1].xyz
mad r1.xyz, cb0[0].xyz, v0.yyy, r1.xyz
mad o1.xyz, cb0[2].xyz, v0.zzz, r1.xyz
mul r1.xyz, |v0.xxx|, cb0[1].xyz
mad r1.xyz, cb0[0].xyz, |v0.yyy|, r1.xyz
mad o2.xyz, cb0[2].xyz, |v0.zzz|, r1.xyz
add r0.w, v0.x, v0.x
mad r0.xyz, cb0[0].xyz, r0.www, r0.xyz
mul r0.w, v0.z, l(3)
mad o3.xyz, cb0[2].xyz, r0.www, r0.xyz
ret
