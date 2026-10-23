ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_texture2dms(0) (float,float,float,float) t0
dcl_input_ps linear v1.xy
dcl_input_ps linear v2
dcl_output o0
dcl_temps 3
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
uge r1.y, r1.x, cb0[0].z
breakc_nz r1.y
samplepos r1.yz, t0.xy, r1.xx
eval_sample_index r2, v2, r1.x
mad r1.y, r1.y, r1.z, l(1)
mad r0, r2, r1.y, r0
iadd r1.x, r1.x, l(1)
endloop
eval_centroid r1.xy, v1.xy
eval_snapped r1.zw, v1.xy, cb0[0].xx
utof r2.x, cb0[0].z
div r0, r0, r2.x
add o0, r1, r0
ret
