ps_4_0
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
mul r0.xy, v0.xy, l(512, 512, 0, 0)
ftou r0.xy, r0.xy
resinfo_uint r1, l(0), t0
udiv null, r0.xy, r0.xy, r1.xy
mov r0.zw, l(0, 0, 0, 0)
ld o0, r0, t0
ret
