ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
mov r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
loop
ige r1.x, r0.w, l(16)
breakc_nz r1.x
sample_l r1, r0.xyxx, t0, s0, l(0)
mad r0.z, r1.x, cb0[0].x, r0.z
lt r1.x, cb0[0].y, r0.z
if_nz r1.x
itof o0.y, r0.w
mov o0.zw, l(0, 0, 1, 1)
mov o0.x, r0.z
ret
endif
add r0.xy, r0.xy, cb0[0].zw
iadd r0.w, r0.w, l(1)
endloop
mov o0.x, r0.z
mov o0.yzw, l(0, 16, 0, 1)
ret
