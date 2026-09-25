hs_5_0
hs_decls
dcl_input_control_point_count 4
dcl_output_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_ccw
dcl_hs_max_tessfactor l(32)
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[4][0].xyz
dcl_input v[4][1].xy
dcl_output o0.xyz
dcl_output o1.xy
dcl_temps 1
mov r0.x, vOutputControlPointID
add o0.xyz, v[r0.x][0].xyz, v[r0.x][0].xyz
mov o1.xy, v[r0.x][1].xy
ret
hs_fork_phase
dcl_output_siv o0.x, finalQuadUeq0EdgeTessFactor
min o0.x, cb0[0].x, l(32)
ret
hs_fork_phase
dcl_output_siv o1.x, finalQuadVeq0EdgeTessFactor
min o1.x, cb0[0].y, l(32)
ret
hs_fork_phase
dcl_output_siv o2.x, finalQuadUeq1EdgeTessFactor
min o2.x, cb0[0].z, l(32)
ret
hs_fork_phase
dcl_output_siv o3.x, finalQuadVeq1EdgeTessFactor
min o3.x, cb0[0].w, l(32)
ret
hs_fork_phase
dcl_input vicp[4][0].y
dcl_output_siv o4.x, finalQuadUInsideTessFactor
dcl_temps 1
add r0.x, cb0[1].x, vicp[0][0].y
min o4.x, r0.x, l(32)
ret
hs_fork_phase
dcl_output_siv o5.x, finalQuadVInsideTessFactor
min o5.x, cb0[1].y, l(32)
ret
