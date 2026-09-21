ps_4_1
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[13], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2darray (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_input_ps linear v1
dcl_output o0
dcl_temps 4
if_nz cb0[12].x
mov r0.xy, v0.xy
mov r0.zw, l(0, 0, 1, 1)
dp4 r1.x, r0.xyww, cb0[0]
dp4 r1.y, r0.xyww, cb0[1]
dp4 r1.z, r0.xyww, cb0[2]
lt r2.xyz, |r1.xyz|, l(0.5, 0.5, 0.5, 0)
and r1.z, r2.y, r2.x
and r1.z, r2.z, r1.z
if_nz r1.z
add r1.xy, r1.xy, l(0.5, 0.5, 0, 0)
mov r1.z, l(0)
sample r1, r1.xyzx, t0, s0
add r1.xyz, r1.xyz, -v1.xyz
mad r1.xyz, r1.www, r1.xyz, v1.xyz
else
mov r1.xyz, v1.xyz
endif
uge r2.x, l(1), cb0[12].x
if_z r2.x
dp4 r3.x, r0.xyww, cb0[4]
dp4 r3.y, r0.xyww, cb0[5]
dp4 r3.z, r0, cb0[6]
lt r0.xyz, |r3.xyz|, l(0.5, 0.5, 0.5, 0)
and r0.x, r0.y, r0.x
and r0.x, r0.z, r0.x
if_nz r0.x
add r0.xy, r3.xy, l(0.5, 0.5, 0, 0)
mov r0.z, l(1)
sample r0, r0.xyzx, t0, s0
add r0.xyz, -r1.xyz, r0.xyz
mad r1.xyz, r0.www, r0.xyz, r1.xyz
endif
endif
else
mov r1.xyz, v1.xyz
mov r2.x, l(-1)
endif
if_z r2.x
ult r0.x, l(2), cb0[12].x
if_nz r0.x
mov r0.xy, v0.xy
mov r0.zw, l(0, 0, 1, 1)
dp4 r2.x, r0.xyww, cb0[8]
dp4 r2.y, r0.xyww, cb0[9]
dp4 r2.z, r0, cb0[10]
lt r0.xyz, |r2.xyz|, l(0.5, 0.5, 0.5, 0)
and r0.x, r0.y, r0.x
and r0.x, r0.z, r0.x
if_nz r0.x
add r0.xy, r2.xy, l(0.5, 0.5, 0, 0)
mov r0.z, l(2)
sample r0, r0.xyzx, t0, s0
add r0.xyz, -r1.xyz, r0.xyz
mad r1.xyz, r0.www, r0.xyz, r1.xyz
endif
endif
endif
mov r1.w, v1.w
mov o0, r1
ret
