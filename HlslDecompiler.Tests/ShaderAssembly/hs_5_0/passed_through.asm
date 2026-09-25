hs_5_0
hs_decls
dcl_input_control_point_count 4
dcl_output_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_tessellator_partitioning partitioning_pow2
dcl_tessellator_output_primitive output_point
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_fork_phase
dcl_input vicp[4][0].x
dcl_output_siv o0.x, finalQuadUeq0EdgeTessFactor
add o0.x, cb0[0].x, vicp[0][0].x
ret
hs_fork_phase
dcl_input vicp[4][0].x
dcl_output_siv o1.x, finalQuadVeq0EdgeTessFactor
add o1.x, cb0[0].y, vicp[1][0].x
ret
hs_fork_phase
dcl_input vicp[4][0].x
dcl_output_siv o2.x, finalQuadUeq1EdgeTessFactor
add o2.x, cb0[0].z, vicp[2][0].x
ret
hs_fork_phase
dcl_input vicp[4][0].x
dcl_output_siv o3.x, finalQuadVeq1EdgeTessFactor
add o3.x, cb0[0].w, vicp[3][0].x
ret
hs_fork_phase
dcl_output_siv o4.x, finalQuadUInsideTessFactor
mov o4.x, cb0[0].x
ret
hs_fork_phase
dcl_output_siv o5.x, finalQuadVInsideTessFactor
mov o5.x, cb0[0].y
ret
