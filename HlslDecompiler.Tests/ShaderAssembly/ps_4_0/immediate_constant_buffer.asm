ps_4_0
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer cb0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 3
mov r0.xy, v0.xy
mov r1, l(0, 0, 0, 0)
mov r0.w, l(0)
loop
ige r2.x, r0.w, l(4)
breakc_nz r2.x
itof r0.z, r0.w
sample r2, r0.xyzx, t0, s0
dp4 r0.z, cb0[0], icb[r0.w]
mad r1, r2, r0.z, r1
iadd r0.w, r0.w, l(1)
endloop
mov o0, r1
ret
