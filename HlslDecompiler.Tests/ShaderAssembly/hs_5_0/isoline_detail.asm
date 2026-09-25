hs_5_0
hs_decls
dcl_input_control_point_count 2
dcl_output_control_point_count 2
dcl_tessellator_domain domain_isoline
dcl_tessellator_partitioning partitioning_fractional_even
dcl_tessellator_output_primitive output_line
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], immediateIndexed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[2][0].xyz
dcl_input v[2][0].w
dcl_output o0.xyz
dcl_output o0.w
dcl_temps 1
mov r0.x, vOutputControlPointID
mul o0.w, l(0.5), v[r0.x][0].w
mov r0.xyz, v[r0.x][0].xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
ret
hs_fork_phase
dcl_input vicp[2][0].w
dcl_output_siv o0.x, finalLineDensityTessFactor
dcl_temps 1
add r0.x, vicp[1][0].w, vicp[0][0].w
mul o0.x, r0.x, cb0[4].x
ret
hs_fork_phase
dcl_input vPrim
dcl_output_siv o1.x, finalLineDetailTessFactor
dcl_temps 1
utof r0.x, vPrim
add o1.x, r0.x, cb0[4].y
ret
