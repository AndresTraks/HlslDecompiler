ps_4_0
dcl_constantbuffer CB0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t3
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sample r0, v0.xyxx, t3, s0
if_nz cb0[0].x
add r0.x, r0.x, cb0[1].x
uge r1.x, l(1), cb0[0].x
if_z r1.x
add r0.y, r0.y, cb0[1].y
endif
else
mov r1.x, l(-1)
endif
if_z r1.x
add r0.z, r0.z, cb0[1].z
endif
mov o0, r0
ret
